using System;
using System.Web.Http;
using System.Xml;
using NLog;
using XmlReceiver.Core;
using XmlReceiver.Models;
using XmlReceiver.Models.Response;

namespace XmlReceiver.Controllers
{
    public class UploadXmlController : ApiController
    {
        private Logger Log => LogManager.GetCurrentClassLogger();

        private SqlTableInfo ParseXml(string xml)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);

                if (xmlDoc.DocumentElement == null)
                    throw new Exception("Empty DocumentElement");

                return new SqlTableInfo
                {
                    TableName = xmlDoc.DocumentElement.Name,
                    Fields = FieldDictionary.LoadFromXml(xmlDoc.DocumentElement.SelectNodes("Field")),
                    Row = RowDictionary.LoadFromXmlRoot(xmlDoc.DocumentElement)
                };
            }
            catch (Exception e)
            {
                throw new ApiExc(ErrorEnum.ERR_XML_PARSE, e.Message);
            }
        }



        [HttpPost]
        [Route("api/uploadxml")]
        public ApiSuccess UploadXml()
        {
            var xml = Request.Content.ReadAsStringAsync().Result;
            Log.Debug($"Incoming file{Environment.NewLine}{xml}");

            var tableInfo = ParseXml(xml);
            DbHelper.UpdateTableStructure(tableInfo);
            DbHelper.AddRow(tableInfo);
            return new ApiSuccess { Status = "OK" };
        }
    }
}
