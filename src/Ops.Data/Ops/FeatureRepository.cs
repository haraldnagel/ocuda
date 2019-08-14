﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.Extensions;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Models;
using Ocuda.Promenade.Models.Entities;

namespace Ocuda.Ops.Data.Ops
{
    public class FeatureRepository : GenericRepository<PromenadeContext, Feature, int>, IFeatureRepository
    {
        public FeatureRepository(ServiceFacade.Repository<PromenadeContext> repositoryFacade,
            ILogger<LinkRepository> logger) : base(repositoryFacade, logger)
        {
        }

        public async Task<List<Feature>> GeAllFeaturesAsync()
        {
            return await DbSet
                .AsNoTracking()
                .OrderBy(_ => _.Name)
                .ToListAsync();
        }

        public async Task<DataWithCount<ICollection<Feature>>> GetPaginatedListAsync(
            BaseFilter filter)
        {
            return new DataWithCount<ICollection<Feature>>
            {
                Count = await DbSet.AsNoTracking().CountAsync(),
                Data = await DbSet.AsNoTracking()
                    .OrderBy(_ => _.Name)
                    .ApplyPagination(filter)
                    .ToListAsync()
            };
        }


        public async Task<Feature> GetFeatureByName(string featureName)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.Name.ToLower() == featureName.ToLower())
                .FirstOrDefaultAsync();
        }

        public async Task<bool> IsDuplicateNameAsync(Feature feature)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.Name == feature.Name && _.Id != feature.Id)
                .AnyAsync();
        }

        public async Task<bool> IsDuplicateStubAsync(Feature feature)
        {
            return await DbSet
                .AsNoTracking()
                .Where(_ => _.Id != feature.Id && _.Stub == feature.Stub)
                .AnyAsync();
        }
    }
}