﻿namespace Ocuda.Promenade.Service.Filters
{
    public class BaseFilter
    {
        public int? Skip { get; set; }
        public int? Take { get; set; }

        public BaseFilter(int? page = null, int take = 15)
        {
            Take = take;
            Skip = page.HasValue ? Take * (page - 1) : 0;
        }
    }
}
