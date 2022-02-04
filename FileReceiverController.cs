using CrunchTime.Sql;
using MultipartDataMediaFormatter.Infrastructure;
using NLog;
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;

namespace CrunchTime.Controllers
{
    public class FileReceiverController : ApiController
    {
        public string GetUserIp(HttpRequestMessage request)
        {
            if (request.Properties.ContainsKey("MS_HttpContext"))
            {
                var ctx = request.Properties["MS_HttpContext"] as HttpContextBase;
                if (ctx != null)
                {
                    return ctx.Request.UserHostAddress;
                }
            }

            return null;
        }


        [HttpPost]
        [Route("api/files")]
        public HttpResponseMessage PostRawData(HttpRequestMessage request, FormData formData)
        {
         



            var files = formData.GetFiles("filename", CultureInfo.GetCultureInfo("en-US"));

            if (files.Count == 0)
            {
                LogManager.GetCurrentClassLogger().Debug("Incoming file do not contains filename");
                return new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent("Incoming file do not contains filename or empty request", Encoding.UTF8) };
            }

            var savePath = ConfigurationManager.AppSettings["SavePath"].EndsWith(Path.DirectorySeparatorChar.ToString())
                ? ConfigurationManager.AppSettings["SavePath"]
                : ConfigurationManager.AppSettings["SavePath"] + Path.DirectorySeparatorChar;


            foreach (var file in files)
            {
                var fullPath = Path.Combine(savePath, file.FileName);

                LogManager.GetCurrentClassLogger().Debug($"Incoming FormData : {file.FileName}");

                File.WriteAllBytes(fullPath, file.Buffer);
            }


            //в БД можем и не положить данные...(
            try
            {
                var clientIp = GetUserIp(request);

                using (var sql = MsSql.OpenFromDefaultConfig())
                {
                    foreach (var file in files)
                    {
                        using (var cmd = sql.CreateCommand(Queries.InsertUpload))
                        {
                            cmd.Parameters.AddWithValue("@guid", Guid.NewGuid());
                            cmd.Parameters.AddWithValue("@FileFormat", Path.GetExtension(file.FileName));
                            cmd.Parameters.AddWithValue("@Client", clientIp);
                            cmd.Parameters.AddWithValue("@DTS", DateTime.Now);
                            cmd.Parameters.AddWithValue("@FileName", file.FileName);
                            cmd.ExecuteNonQuery();
                        }
                    }                
                }
            }
            catch (Exception e)
            {
                LogManager.GetCurrentClassLogger().Debug($"Sql write error: {e.Message}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        [Route("api/v2/files")]
        public HttpResponseMessage PostRawDataV2(HttpRequestMessage request, FormData formData)
        {
            var clientIp0 = GetUserIp(request);


            var files = formData.Files;

            if (files.Count == 0)
            {
                LogManager.GetCurrentClassLogger().Debug("Incoming file do not contains filename");
                return new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent("Incoming file do not contains filename or empty request", Encoding.UTF8) };
            }

            var savePath = ConfigurationManager.AppSettings["SavePath"].EndsWith(Path.DirectorySeparatorChar.ToString())
                ? ConfigurationManager.AppSettings["SavePath"]
                : ConfigurationManager.AppSettings["SavePath"] + Path.DirectorySeparatorChar;


            foreach (var file in files)
            {
                var fullPath = Path.Combine(savePath, file.Value.FileName);

                LogManager.GetCurrentClassLogger().Debug($"Incoming FormData : {file.Value.FileName}");

                File.WriteAllBytes(fullPath, file.Value.Buffer);
            }


            //в БД можем и не положить данные...(
            try
            {
                var clientIp = GetUserIp(request);

                using (var sql = MsSql.OpenFromDefaultConfig())
                {
                    foreach (var file in files)
                    {
                        using (var cmd = sql.CreateCommand(Queries.InsertUpload))
                        {
                            cmd.Parameters.AddWithValue("@guid", Guid.NewGuid());
                            cmd.Parameters.AddWithValue("@FileFormat", Path.GetExtension(file.Value.FileName));
                            cmd.Parameters.AddWithValue("@Client", clientIp);
                            cmd.Parameters.AddWithValue("@DTS", DateTime.Now);
                            cmd.Parameters.AddWithValue("@FileName", file.Value.FileName);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogManager.GetCurrentClassLogger().Debug($"Sql write error: {e.Message}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}