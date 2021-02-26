using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;


namespace EncouraDataLabs
{
    class Program
    {

        //Global Variables
        private static String EncouraUrl = "https://api.datalab.nrccua.org/v1/login";
        private static String EncouraApiKey = ""; //Your Encoura API Key
        private static String EncouraOrganizationUid = ""; //Your Encoura Organization UID
        private static String DownloadDirectory = ""; // Destination path of the downloaded file
        private static String EncouraUsername = ""; // Your Encoura Username
        private static String EncouraPassword = ""; // Your Encoura Password
        private static String strSessionToken = "";
        
        //Set true to show debugging output, false to hide
        private const bool DEBUG = true;

        static void Main(string[] args)
        {

            //Force use of TLS1.2
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;


            /**********************
            **  Get sessionToken **
            ***********************/
            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpRequestMessage authenticationRequest = new HttpRequestMessage(new HttpMethod("POST"), EncouraUrl))
                {
                    //Set default headers for the remainder of the session
                    httpClient.DefaultRequestHeaders.Add("x-api-key", EncouraApiKey);
                    httpClient.DefaultRequestHeaders.Add("Organization", EncouraOrganizationUid);

                    //Add payload to body
                    authenticationRequest.Content = new StringContent("{\"userName\":\"" + EncouraUsername + "\",\"password\":\"" + EncouraPassword + "\",\"acceptedTerms\":true}", Encoding.UTF8, "application/json");

                    //Send request
                    HttpResponseMessage response = httpClient.SendAsync(authenticationRequest).Result;

                    //Parse JSON response
                    string strResponseString = JsonConvert.SerializeObject(response.Content.ReadAsStringAsync(), Formatting.Indented);
                    JObject objResponseJson = JObject.Parse(strResponseString);
                    string strAuthResponse = objResponseJson["Result"].ToString();

                    if (strAuthResponse.Contains("sessionToken"))
                    {
                        //Parse JSON and retrieve sessionToken
                        JObject objJsonDetails = JObject.Parse(strAuthResponse);
                        string strSessionTokenJson = objJsonDetails["sessionToken"].ToString();
                        strSessionToken = strSessionTokenJson.TrimStart('"').TrimEnd('"'); //Once in a while I received the session token in quotes

                        //Add sessionToken to the default httpClient headers
                        httpClient.DefaultRequestHeaders.Add("Authorization", "JWT " + strSessionToken);

                        /*******************************
                        **  Get filenames to download **
                        ********************************/
                        using (HttpRequestMessage fileNameRequest = new HttpRequestMessage(new HttpMethod("GET"), "https://api.datalab.nrccua.org/v1/datacenter/exports?productKey=score-reporter&status=NotDelivered"))
                        {
                            //Sent request
                            HttpResponseMessage fileNameResponse = httpClient.SendAsync(fileNameRequest).Result;

                            //Parse JSON to retrieve download URLs
                            string strFileNameResponseString = JsonConvert.SerializeObject(fileNameResponse.Content.ReadAsStringAsync(), Formatting.Indented);
                            JObject objFileNameResponseJson = JObject.Parse(strFileNameResponseString);
                            string result = objFileNameResponseJson["Result"].ToString();

                            if (result.ToString() != "asdf")
                            {
                                if (DEBUG) Debug.WriteLine("Files to download");

                                /********************
                                **  Download files **
                                *********************/
                                JObject objFileUid;
                                try
                                {
                                    objFileUid = JObject.Parse(result);
                                }
                                catch
                                {
                                    // Exit script if no files to download
                                    return;
                                }
                                
                                //Get file uid
                                String strFileUid = objFileUid["uid"].ToString();
                                using (HttpRequestMessage fileDownloadRequest = new HttpRequestMessage(new HttpMethod("GET"), "https://api.datalab.nrccua.org/v1/datacenter/exports/" + strFileUid + "/download/"))
                                {
                                    fileDownloadRequest.Headers.Add("x-api-key", EncouraApiKey);
                                    fileDownloadRequest.Headers.Add("Authorization", "JWT " + strSessionToken);
                                    fileDownloadRequest.Headers.Add("Organization", EncouraOrganizationUid);

                                    //Get URL from file uid
                                    HttpResponseMessage fileDownloadResponse = httpClient.SendAsync(fileDownloadRequest).Result;
                                
                                    string strDownloadResponseString = JsonConvert.SerializeObject(fileDownloadResponse.Content.ReadAsStringAsync(), Formatting.Indented);
                                    JObject objDownloadResponseJson = JObject.Parse(strDownloadResponseString);
                                    String strDownloadJsonResponse = objDownloadResponseJson["Result"].ToString();

                                    if (strDownloadJsonResponse.Contains("downloadUrl"))
                                    {
                                        if (DEBUG) Debug.WriteLine("urlresponse = " + strDownloadJsonResponse);
                                        //Extract download url from strDownloadJsonResponse
                                        JObject objDownloadUrlJson = JObject.Parse(strDownloadJsonResponse.Replace(@"\", string.Empty).TrimStart('"').TrimEnd('"'));
                                        string strDownloadUrl = objDownloadUrlJson["downloadUrl"].ToString();

                                        if (DEBUG) Debug.WriteLine("URL = " + strDownloadUrl);
   
                                        //Submit web request to download and store the file.
                                        using (WebClient wc = new WebClient())
                                        {
                                            string downloadPath = Path.GetFullPath(DownloadDirectory);
                                            Uri uri = new Uri(strDownloadUrl);
                                            //Get original file name
                                            String filePath = downloadPath + System.IO.Path.GetFileName(uri.LocalPath);
                                            //Download and store the file
                                            wc.DownloadFile(new System.Uri(strDownloadUrl), (filePath).ToString());
                                        }
                                    }
                                    else
                                    {
                                        if (DEBUG)Debug.WriteLine("No file to download");
                                    }

                                }
                            }
                        }

                    }
                    else
                    {
                        if (DEBUG) Debug.WriteLine("Unable to attain session token");
                    }

                }
            }
        }




    }
}


