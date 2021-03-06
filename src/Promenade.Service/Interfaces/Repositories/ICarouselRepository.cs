﻿using System.Threading.Tasks;
using Ocuda.Promenade.Models.Entities;

namespace Ocuda.Promenade.Service.Interfaces.Repositories
{
    public interface ICarouselRepository : IGenericRepository<Carousel>
    {
        Task<Carousel> GetIncludingChildrenAsync(int id);
    }
}
