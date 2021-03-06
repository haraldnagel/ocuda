﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Abstract;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Utility.Email;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Services.Interfaces;

namespace Ocuda.Ops.Service
{
    public class ScheduleNotificationService
        : BaseService<ScheduleNotificationService>, IScheduleNotificationService
    {
        private const int CacheEmailHours = 1;
        private const string DefaultLanguage = "en-US";
        private readonly IOcudaCache _cache;
        private readonly IEmailService _emailService;
        private readonly IScheduleLogRepository _scheduleLogRepository;
        private readonly IScheduleRequestService _scheduleRequestService;
        private readonly ISiteSettingService _siteSettingService;

        public ScheduleNotificationService(ILogger<ScheduleNotificationService> logger,
            IHttpContextAccessor httpContextAccessor,
            IOcudaCache cache,
            IEmailService emailService,
            IScheduleLogRepository scheduleLogRepsitory,
            IScheduleRequestService scheduleRequestService,
            ISiteSettingService siteSettingService) : base(logger, httpContextAccessor)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _scheduleLogRepository = scheduleLogRepsitory
                ?? throw new ArgumentNullException(nameof(scheduleLogRepsitory));
            _scheduleRequestService = scheduleRequestService
                ?? throw new ArgumentNullException(nameof(scheduleRequestService));
            _siteSettingService = siteSettingService
                ?? throw new ArgumentNullException(nameof(siteSettingService));
        }

        private enum EmailType
        {
            ScheduleNotification,
            Followup,
            Cancellation
        }

        public async Task<Configuration> GetEmailSettingsAsync()
        {
            var config = new Configuration
            {
                BccAddress = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.BccAddress),
                FromAddress = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.FromAddress),
                FromName = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.FromName),
                OutgoingHost = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.OutgoingHost),
                OutgoingLogin = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.OutgoingLogin),
                OutgoingPassword = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.OutgoingPassword),
                OutgoingPort = await _siteSettingService
                    .GetSettingIntAsync(Ops.Models.Keys.SiteSetting.Email.OutgoingPort),
                OverrideToAddress = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.OverrideToAddress),
                RestrictToDomain = await _siteSettingService
                    .GetSettingStringAsync(Ops.Models.Keys.SiteSetting.Email.RestrictToDomain)
            };

            if (string.IsNullOrEmpty(config.FromAddress))
            {
                throw new OcudaEmailException("From address is not configured");
            }

            if (string.IsNullOrEmpty(config.FromName))
            {
                throw new OcudaEmailException("From name is not configured");
            }

            if (string.IsNullOrEmpty(config.OutgoingHost))
            {
                throw new OcudaEmailException("Outgoing mail host is not configured");
            }

            return config;
        }

        public Task<EmailRecord> SendCancellationAsync(ScheduleRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            return SendCancellationInternalAsync(request);
        }

        public Task<bool> SendFollowupAsync(ScheduleRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            return SendFollowupInternalAsync(request);
        }

        public async Task<int> SendPendingNotificationsAsync()
        {
            int sentNotifications = 0;

            try
            {
                var pendingNotifications
                    = await _scheduleRequestService.GetPendingNotificationsAsync();

                if (pendingNotifications?.Count > 0)
                {
                    _logger.LogDebug("Found {PendingNotificationCount} pending notification(s)",
                        pendingNotifications.Count);

                    Configuration settings = null;
                    try
                    {
                        settings = await GetEmailSettingsAsync();
                    }
                    catch (OcudaEmailException oex)
                    {
                        _logger.LogError("Error finding email settings: {ErrorMessage}", oex.Message);
                    }

                    if (settings != null)
                    {
                        foreach (var pending in pendingNotifications)
                        {
                            try
                            {
                                var lang = pending.Language
                                .Equals("English", StringComparison.OrdinalIgnoreCase)
                                    ? DefaultLanguage
                                    : pending.Language;
                                _logger.LogTrace("Using language: {Language}", pending.Language);

                                var culture = CultureInfo.GetCultureInfo(lang);
                                _logger.LogTrace("Found culture: {Culture}", culture.DisplayName);

                                var sentEmail = await SendAsync(pending,
                                    settings,
                                    lang,
                                    new Dictionary<string, string>
                                    {
                                    { "ScheduledDate",
                                        pending.RequestedTime.ToString("d", culture) },
                                    { "ScheduledTime",
                                        pending.RequestedTime.ToString("t", culture) },
                                    { "Scheduled", pending.RequestedTime.ToString("g", culture) },
                                    { "Subject", pending.ScheduleRequestSubject.Subject }
                                    },
                                    EmailType.ScheduleNotification);

                                if (sentEmail != null)
                                {
                                    sentNotifications++;

                                    await _scheduleRequestService.SetNotificationSentAsync(pending);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Sending pending notification id {RequestId} failed: {ErrorMessage}",
                                    pending.Id,
                                    ex.Message);
                            }

                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                }
            }
            catch (OcudaException oex)
            {
                _logger.LogError(oex,
                    "Uncaught error sending notifications: {ErrorMessage}",
                    oex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Uncaught critical error sending notifications: {ErrorMessage}",
                    ex.Message);
            }

            if (sentNotifications > 0)
            {
                _logger.LogInformation("Scheduled task sent {NotificationCount} email notifications",
                    sentNotifications);
            }

            return sentNotifications;
        }

        private async Task<EmailRecord> SendAsync(ScheduleRequest request,
            Configuration settings,
            string lang,
            Dictionary<string, string> tagDictionary,
            EmailType emailType)
        {
            int? setupIdLookup = 0;
            string emailDescription = null;

            switch (emailType)
            {
                case EmailType.ScheduleNotification:
                    setupIdLookup = (int)request.ScheduleRequestSubject.RelatedEmailSetupId;
                    emailDescription = "Request confirmation email";
                    break;

                case EmailType.Followup:
                    setupIdLookup = (int)request.ScheduleRequestSubject.FollowupEmailSetupId;
                    emailDescription = "Follow-up email";
                    break;

                case EmailType.Cancellation:
                    setupIdLookup = (int)request.ScheduleRequestSubject.CancellationEmailSetupId;
                    emailDescription = "Cancellation email";
                    break;
            }

            if (setupIdLookup == null)
            {
                return null;
            }

            int setupId = (int)setupIdLookup;

            var emailSetupCacheKey = string.Format(CultureInfo.InvariantCulture,
                Utility.Keys.Cache.OpsEmailSetup,
                setupId,
                lang);
            var emailSetupText
                = await _cache.GetObjectFromCacheAsync<EmailSetupText>(emailSetupCacheKey);
            var emailSetupFromCache = emailSetupText != null;

            if (!emailSetupFromCache)
            {
                emailSetupText = await _emailService.GetEmailSetupAsync(setupId, lang);

                if (emailSetupText == null)
                {
                    // perhaps we didn't match the culture, try default
                    emailSetupText = await _emailService.GetEmailSetupAsync(setupId,
                        DefaultLanguage);
                    _logger.LogWarning("Email setup id {EmailSetupId} not found for language {Language}, {FoundOrNot} for default language {DefaultLanguage}",
                        setupId,
                        lang,
                        emailSetupText == null ? "not found" : "found",
                        DefaultLanguage);
                }

                if (emailSetupText == null)
                {
                    throw new OcudaException($"Missing email setup ID {setupId}");
                }

                await _cache.SaveToCacheAsync(emailSetupCacheKey,
                    emailSetupText,
                    CacheEmailHours);
            }

            _logger.LogTrace("Email setup for language {Language}{Cached}: HTML {HtmlLength} chars, text {TextLength} chars",
                emailSetupText.PromenadeLanguageName,
                emailSetupFromCache ? " (cached)" : "",
                emailSetupText.BodyHtml.Length,
                emailSetupText.BodyText.Length);

            var emailTemplateCacheKey = string.Format(
                CultureInfo.InvariantCulture,
                Utility.Keys.Cache.OpsEmailTemplate,
                emailSetupText.EmailSetup.EmailTemplateId,
                lang);
            var emailTemplateText
                = await _cache.GetObjectFromCacheAsync<EmailTemplateText>(emailTemplateCacheKey);
            var emailTemplateFromCache = emailTemplateText != null;

            if (!emailTemplateFromCache)
            {
                emailTemplateText = await _emailService
                 .GetEmailTemplateAsync(emailSetupText.EmailSetup.EmailTemplateId, lang);

                if (emailTemplateText == null)
                {
                    // perhaps we didn't match the culture, try default
                    emailTemplateText = await _emailService.GetEmailTemplateAsync(
                        emailSetupText.EmailSetup.EmailTemplateId,
                        DefaultLanguage);

                    _logger.LogWarning("Email template id {EmailTemplateId} not found for language {Language}, {FoundOrNot} for default language {DefaultLanguage}",
                        emailSetupText.EmailSetup.EmailTemplateId,
                        lang,
                        emailSetupText == null ? "not found" : "found",
                        DefaultLanguage);
                }

                if (emailTemplateText == null)
                {
                    throw new OcudaException($"Missing email template ID {emailSetupText.EmailSetup.EmailTemplateId}");
                }

                await _cache.SaveToCacheAsync(emailTemplateCacheKey,
                    emailTemplateText,
                    CacheEmailHours);
            }

            _logger.LogTrace("Email template{Cached}: HTML {HtmlLength} chars, text {TextLength} chars",
                emailTemplateFromCache ? " (cached)" : "",
                emailTemplateText.TemplateHtml.Length,
                emailTemplateText.TemplateText.Length);

            var emailDetails = new Details
            {
                FromEmailAddress = emailSetupText.EmailSetup.FromEmailAddress,
                FromName = emailSetupText.EmailSetup.FromName,
                ToEmailAddress = request.Email.Trim(),
                ToName = request.Name?.Trim(),
                UrlParameters = emailSetupText.UrlParameters,
                Preview = emailSetupText.Preview,
                TemplateHtml = emailTemplateText.TemplateHtml,
                TemplateText = emailTemplateText.TemplateText,
                BodyText = emailSetupText.BodyText,
                BodyHtml = emailSetupText.BodyHtml,
                Tags = tagDictionary,
                BccEmailAddress = settings.BccAddress,
                OverrideEmailToAddress = settings.OverrideToAddress,
                Password = settings.OutgoingPassword,
                Port = settings.OutgoingPort,
                RestrictToDomain = settings.RestrictToDomain,
                Server = settings.OutgoingHost,
                Subject = emailSetupText.Subject,
                Username = settings.OutgoingLogin
            };

            try
            {
                _logger.LogTrace("{EmailDescription} (setup {EmailSetupId}) sending to {EmailTo}...",
                    emailDescription,
                    emailSetupText.EmailSetup.Id,
                    request.Email.Trim());

                var sentEmail = await _emailService.SendAsync(emailDetails);

                if (sentEmail != null)
                {
                    _logger.LogInformation("{EmailDescription} (setup {EmailSetupId}) sent to {EmailTo}",
                        emailDescription,
                        emailSetupText.EmailSetup.Id,
                        request.Email.Trim());

                    await _scheduleRequestService.SetFollowupSentAsync(request);

                    await _scheduleLogRepository.AddAsync(new ScheduleLog
                    {
                        CreatedAt = DateTime.Now,
                        Notes = $"{emailDescription} sent to {request.Email.Trim()}.",
                        ScheduleRequestId = request.Id,
                        RelatedEmailId = sentEmail?.Id
                    });
                    await _scheduleLogRepository.SaveAsync();

                    return sentEmail;
                }
                else
                {
                    _logger.LogWarning("{EmailDescription} (setup {EmailSetupId}) failed sending to {EmailTo}",
                        emailDescription,
                        emailSetupText.EmailSetup.Id,
                        request.Email.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending email setup {EmailSetupId} to {EmailTo}: {ErrorMessage}",
                    emailSetupText.EmailSetup.Id,
                    request.Email.Trim(),
                    ex.Message);
            }

            return null;
        }

        private async Task<EmailRecord> SendCancellationInternalAsync(ScheduleRequest request)
        {
            Configuration settings = null;
            try
            {
                settings = await GetEmailSettingsAsync();
            }
            catch (OcudaEmailException)
            {
                _logger.LogTrace("Sending emails is not configured.");
            }

            if (settings != null)
            {
                var lang = request.Language
                    .Equals("English", StringComparison.OrdinalIgnoreCase)
                        ? "en-US"
                        : request.Language;

                var culture = CultureInfo.GetCultureInfo(lang);

                try
                {
                    return await SendAsync(request,
                        settings,
                        lang,
                        new Dictionary<string, string>
                        {
                            { "Scheduled", request.RequestedTime.ToString(culture) },
                            { "Subject", request.ScheduleRequestSubject.Subject }
                        },
                        EmailType.Cancellation);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Sending cancellation notification id {RequestId} failed: {ErrorMessage}",
                        request.Id,
                        ex.Message);
                }
            }
            return null;
        }

        private async Task<bool> SendFollowupInternalAsync(ScheduleRequest request)
        {
            Configuration settings = null;
            try
            {
                settings = await GetEmailSettingsAsync();
            }
            catch (OcudaEmailException)
            {
                _logger.LogTrace("Sending emails is not configured.");
            }

            if (settings != null)
            {
                var lang = request.Language
                    .Equals("English", StringComparison.OrdinalIgnoreCase)
                        ? "en-US"
                        : request.Language;

                var culture = CultureInfo.GetCultureInfo(lang);
                try
                {
                    var sentEmail = await SendAsync(request,
                        settings,
                        lang,
                        new Dictionary<string, string>
                        {
                            { "Scheduled", request.RequestedTime.ToString(culture) },
                            { "Subject", request.ScheduleRequestSubject.Subject }
                        },
                        EmailType.Followup);

                    if (sentEmail != null)
                    {
                        await _scheduleRequestService.SetFollowupSentAsync(request);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Sending followup notification id {RequestId} failed: {ErrorMessage}",
                        request.Id,
                        ex.Message);
                }
            }
            return false;
        }
    }
}