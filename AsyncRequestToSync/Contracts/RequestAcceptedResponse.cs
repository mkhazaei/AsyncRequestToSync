using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncRequestToSync.Contracts
{
    public record RequestAcceptedResponse(Guid CorrelationId) : IMessage;
}
