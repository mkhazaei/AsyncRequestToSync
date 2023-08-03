using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRequestToSync
{
    public interface IAsyncConectionHandler
    {
        /// <summary>
        /// 
        /// </summary>
        Task WaitForResponse(HttpContext context, Guid correlationId);
    }
}
