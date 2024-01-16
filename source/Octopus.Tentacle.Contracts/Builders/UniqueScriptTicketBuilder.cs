using System;
using System.Linq;

namespace Octopus.Tentacle.Contracts.Builders
{
    internal class UniqueScriptTicketBuilder
    {
        public ScriptTicket Build()
        {
            var e = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            // Sanitise any non alpha or numeric characters
            e = string.Join("", e.Where(char.IsLetterOrDigit));

            return new ScriptTicket(e);
        }
    }
}