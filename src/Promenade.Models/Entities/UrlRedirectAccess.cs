﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Ocuda.Promenade.Models.Entities
{
    public class UrlRedirectAccess
    {
        public int Id { get; set; }
        public int UrlRedirectId { get; set; }
        public UrlRedirect UrlRedirect { get; set; }
        public DateTime AccessDate { get; set; }
    }
}
