using DigitalStation.Rk7;
using DigitalStation.Rk7.Models;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DigitalStation.Services
{
    public class CommonShiftService
    {
        private readonly Rk7Api _rk7;
        private readonly ILogger<CommonShiftService> _log;
        private readonly AppOptions _opts;

        public CommonShiftService(Rk7Api rk7, IOptions<AppOptions> opts,ILogger<CommonShiftService> log)
        {
            _rk7 = rk7;
            _log = log;
            _opts = opts.Value;
        }

        private async Task<int> GetCashstationIdent(int midIdent)
        {
            var filters = new PropFilters
            {
                {"MainParentIdent",midIdent.ToString() },
                {_opts.CashStationDefinition.PropName,_opts.CashStationDefinition.Value }
            };
            var xml = XmlCreator.GetRefData(RefNames.CASHES, true, filters, "Items.(Ident,Name)");

            var stations = await _rk7.GetRefData<Station>(xml);

            if (!stations.Any())
            {
                throw new Exception($@"Could not define Station with the prop: ""{_opts.CashStationDefinition.PropName}""");
            }

            if (stations.Count > 1)
            {
                throw new Exception(@"Multiple stations was found");
            }

            return stations.First().Ident;
        }

        public async Task CloseCommonShift(Employee employee)
        {
            try
            {
                var data = await _rk7.GetSystemInfo2();

                var stationId = await GetCashstationIdent(data.CashGroup.Id);

                var xml = XmlCreator.CloseCommonShift(employee.Ident, stationId);

                var res = await _rk7.GetDataFromXmlInterface<RK7QueryResultBase>(xml);
            }

            catch (TaskCanceledException e)
            {
                _log.LogError($"CloseCommonShift. {e.Message}");
                throw new WarningException(e.Message);
            }
            catch (Exception e)
            {
                _log.LogError($"CloseCommonShift. {e.Message}");
                throw;
            }
        }
    }
}
