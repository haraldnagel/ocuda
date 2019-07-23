﻿using Microsoft.Extensions.Logging;
using Ocuda.Promenade.Models.Entities;
using Ocuda.Promenade.Service.Interfaces.Repositories;

namespace Ocuda.Promenade.Data.Promenade
{
    public class GroupRepository : GenericRepository<Group, int>, IGroupRepository
    {
        public GroupRepository(PromenadeContext context,
            ILogger<FeatureRepository> logger) : base(context, logger)
        {
        }
    }
}
