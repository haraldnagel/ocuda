﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models.Entities;

namespace Ocuda.Ops.Service.Interfaces.Ops.Services
{
    public interface IUserService
    {
        Task<User> LookupUserAsync(string username);
        Task<User> LookupUserByEmailAsync(string email);
        Task<User> AddUser(User user, int? createdById = null);
        Task<User> EnsureSysadminUserAsync();
        Task<User> GetByIdAsync(int id);
        Task<User> EditNicknameAsync(User user);
        Task LoggedInUpdateAsync(User user);
        Task<User> UpdateRosterUserAsync(int rosterUserId, User user);
        Task<Tuple<string, string>> GetUserInfoById(int id);
        Task<ICollection<User>> GetDirectReportsAsync(int supervisorId);
    }
}
