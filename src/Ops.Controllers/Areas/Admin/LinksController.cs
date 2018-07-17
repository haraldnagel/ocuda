﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Controllers.Abstract;
using Ocuda.Ops.Controllers.Areas.Admin.ViewModels.Links;
using Ocuda.Ops.Controllers.Authorization;
using Ocuda.Ops.Models;
using Ocuda.Ops.Service;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Controllers.Areas.Admin
{
    [Area("Admin")]
    [Authorize(Policy = nameof(SectionManagerRequirement))]
    public class LinksController : BaseController<LinksController>
    {
        private readonly ICategoryService _categoryService;
        private readonly ILinkService _linkService;
        private readonly ISectionService _sectionService;

        public const string DefaultCategoryDisplayName = "[No Category]";

        public LinksController(ServiceFacade.Controller<LinksController> context,
            ICategoryService categoryService,
            ILinkService linkService,
            ISectionService sectionService) : base(context)
        {
            _linkService = linkService ?? throw new ArgumentNullException(nameof(linkService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _sectionService = sectionService ?? throw new ArgumentNullException(nameof(sectionService));
        }

        public async Task<IActionResult> Index(string section, int? categoryId = null, int page = 1)
        {
            var currentSection = await _sectionService.GetByPathAsync(section);
            var itemsPerPage = await _siteSettingService.GetSetting(SiteSettingKey.Pagination.ItemsPerPage);
            int.TryParse(itemsPerPage, out int take);

            var filter = new BlogFilter(page, take)
            {
                SectionId = currentSection.Id,
                CategoryId = categoryId,
                CategoryType = CategoryType.Link
            };

            var linkList = await _linkService.GetPaginatedListAsync(filter);
            var categoryList = await _categoryService.GetBySectionIdAsync(filter);

            var paginateModel = new PaginateModel()
            {
                ItemCount = linkList.Count,
                CurrentPage = page,
                ItemsPerPage = filter.Take.Value
            };

            if (paginateModel.MaxPage > 0 && paginateModel.CurrentPage > paginateModel.MaxPage)
            {
                return RedirectToRoute(
                    new
                    {
                        page = paginateModel.LastPage ?? 1
                    });
            }

            var viewModel = new IndexViewModel()
            {
                PaginateModel = paginateModel,
                Links = linkList.Data,
                Categories = categoryList
            };

            if (categoryId.HasValue)
            {
                var name = (await _categoryService.GetCategoryByIdAsync(categoryId.Value)).Name;
                viewModel.CategoryName = 
                    string.IsNullOrWhiteSpace(name) ? DefaultCategoryDisplayName : name;
            }

            return View(viewModel);
        }

        public async Task<IActionResult> Create(string section)
        {
            var currentSection = await _sectionService.GetByPathAsync(section);

            var filter = new BlogFilter()
            {
                SectionId = currentSection.Id,
                CategoryType = CategoryType.Link
            };

            var categories = await _categoryService.GetBySectionIdAsync(filter);

            var viewModel = new DetailViewModel()
            {
                Action = nameof(Create),
                SectionId = currentSection.Id,
                Categories = categories
            };

            return View("Detail", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Create(DetailViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    model.Link.SectionId = model.SectionId;
                    var newLink = await _linkService.CreateAsync(CurrentUserId, model.Link);
                    ShowAlertSuccess($"Added link: {newLink.Name}");
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating link: {ex}", ex);
                    ShowAlertDanger("Unable to add link: ", ex.Message);
                }
            }

            model.Action = nameof(Create);
            return View("Detail", model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var link = await _linkService.GetByIdAsync(id);

            var filter = new BlogFilter()
            {
                SectionId = link.SectionId,
                CategoryType = CategoryType.Link
            };

            var categories = await _categoryService.GetBySectionIdAsync(filter);

            var viewModel = new DetailViewModel()
            {
                Action = nameof(Edit),
                SectionId = link.SectionId,
                Link = link,
                Categories = categories
            };

            return View("Detail", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(DetailViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var link = await _linkService.EditAsync(model.Link);
                    ShowAlertSuccess($"Updated link: {link.Name}");
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating link: {ex}", ex);
                    ShowAlertDanger("Unable to update link: ", ex.Message);
                }
            }

            model.Action = nameof(Edit);
            return View("Detail", model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(IndexViewModel model)
        {
            try
            {
                await _linkService.DeleteAsync(model.Link.Id);
                ShowAlertSuccess("Link deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting link: {ex}", ex);
                ShowAlertDanger("Unable to delete link: ", ex.Message);
            }

            return RedirectToAction(nameof(Index), new { page = model.PaginateModel.CurrentPage });
        }

        public async Task<IActionResult> Categories(string section, int page = 1)
        {
            var currentSection = await _sectionService.GetByPathAsync(section);

            var itemsPerPage = await _siteSettingService.GetSetting(SiteSettingKey.Pagination.ItemsPerPage);
            int.TryParse(itemsPerPage, out int take);

            var filter = new BlogFilter(page, take)
            {
                SectionId = currentSection.Id,
                CategoryType = CategoryType.Link
            };

            var categoryList = await _categoryService.GetPaginatedCategoryListAsync(filter);

            var paginateModel = new PaginateModel()
            {
                ItemCount = categoryList.Count,
                CurrentPage = page,
                ItemsPerPage = filter.Take.Value
            };

            if (paginateModel.MaxPage > 0 && paginateModel.CurrentPage > paginateModel.MaxPage)
            {
                return RedirectToRoute(
                    new
                    {
                        page = paginateModel.LastPage ?? 1
                    });
            }

            var viewModel = new CategoriesViewModel()
            {
                PaginateModel = paginateModel,
                Categories = categoryList.Data,
                SectionId = currentSection.Id
            };

            return View(viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> CreateCategory(CategoriesViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    model.Category.SectionId = model.SectionId;
                    model.Category.CategoryType = CategoryType.Link;
                    var newCategory = await _categoryService.CreateCategoryAsync(CurrentUserId, model.Category);
                    ShowAlertSuccess($"Added link category: {newCategory.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating link category: {ex}", ex);
                    ShowAlertDanger("Unable to add category: ", ex.Message);
                }
            }

            return RedirectToAction(nameof(Categories), new { page = model.PaginateModel.CurrentPage });
        }

        [HttpPost]
        public async Task<IActionResult> EditCategory(CategoriesViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var category = await _categoryService.EditCategoryAsync(model.Category);
                    ShowAlertSuccess($"Updated link category: {category.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error editing link category: {ex}", ex);
                    ShowAlertDanger("Unable to update category: ", ex.Message);
                }
            }

            return RedirectToAction(nameof(Categories), new { page = model.PaginateModel.CurrentPage });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(CategoriesViewModel model)
        {
            try
            {
                await _categoryService.DeleteCategoryAsync(model.Category.Id);
                ShowAlertSuccess("Link category deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting link category: {ex}", ex);
                ShowAlertDanger("Unable to delete category: ", ex.Message);
            }

            return RedirectToAction(nameof(Categories), new { page = model.PaginateModel.CurrentPage });
        }
    }
}