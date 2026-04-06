using System.Collections.Generic;

namespace DVBARPG.Core.Services
{
    public interface IErrorMapper
    {
        string Map(string code);
    }

    public sealed class ErrorMapper : IErrorMapper
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["map_locked"] = "You have not visited this location yet.",
            ["travel_requirement_not_met"] = "Travel requirements are not met.",
            ["shop_offer_unavailable"] = "Offer unavailable.",
            ["shop_insufficient_funds"] = "Not enough currency.",
            ["inventory_full"] = "Inventory is full."
        };

        public string Map(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return "Unexpected error.";
            }

            return _map.TryGetValue(code, out var mapped) ? mapped : code;
        }
    }
}
