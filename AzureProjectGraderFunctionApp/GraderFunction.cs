using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NUnitLite;
using NUnit.Common;

namespace AzureProjectGraderFunctionApp
{
    public static class GraderFunction
    {
        [FunctionName("AzureGraderFunction")]
        public static async Task<IActionResult> Run(
             [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
             ILogger log, ExecutionContext context)
        {
            log.LogInformation("Start AzureGraderFunction");


            if (req.Method == "GET")
            {
                if (!req.Query.ContainsKey("credentials"))
                {
                    string html = @"
<!DOCTYPE html>

<html lang='en' xmlns='http://www.w3.org/1999/xhtml'>
<head>
    <meta charset='utf-8' />
    <title>Azure Grader</title>
</head>
<body>
    <form id='contact-form' method='post'>
        Azure Credentials<br/>
        <textarea name='credentials' required  rows='15' cols='100'></textarea>
        <br/>
        <button type='submit'>Run Test</button>
    </form>
</body>
</html>";


                    return new ContentResult()
                    {
                        Content = html,
                        ContentType = "text/html",
                        StatusCode = 200,
                    };
                }
                else
                {
                    string credentials = req.Query["credentials"];
                    var xml = await RunUnitTest(log, credentials);
                    return new ContentResult { Content = xml, ContentType = "application/xml", StatusCode = 200 };
                }

            }
            else if (req.Method == "POST")
            {
                log.LogInformation("POST Request");

                log.LogInformation("Form Submit");
                string credentials = req.Form["credentials"];
                if(credentials == null)
                {                  
                    return new ContentResult
                    {
                        Content = $"<result><value>No credentials</value></result>",
                        ContentType = "application/xml",
                        StatusCode = 422
                    };
                }
                var xml = await RunUnitTest(log, credentials);        
                return new ContentResult { Content = xml, ContentType = "application/xml", StatusCode = 200 };
            }

            return new OkObjectResult("ok");
        }


        private static async Task<string> RunUnitTest(ILogger log, string credentials)
        {
            var tempCredentialsFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            await File.WriteAllLinesAsync(tempCredentialsFilePath, new string[] { credentials });

            StringWriter strWriter = new StringWriter();
            Environment.SetEnvironmentVariable("AzureAuthFilePath", tempCredentialsFilePath);
            var autoRun = new AutoRun();
            var returnCode = autoRun.Execute(new string[]{
                           "/test:AzureProjectGrader",
                           "--work=" + Path.GetTempPath()
                       }, new ExtendedTextWrapper(strWriter), Console.In);
            log.LogInformation("AutoRun return code:" + returnCode);
            var xml = File.ReadAllText(Path.Combine(Path.GetTempPath(), "TestResult.xml"));
            return xml;
        }
    }
}
