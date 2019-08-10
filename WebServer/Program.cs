using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace WebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ServerConfig serverConfig = new ServerConfig();
            serverConfig.AddApp("local1.com", "C:\\Users\\rjaiswal\\source\\htdocs\\local1.com");
            serverConfig.AddApp("local2.com", "C:\\Users\\rjaiswal\\source\\htdocs\\local2.com");
            Listener server = new Listener(serverConfig);
            server.Start();
        }
    }
    public class Listener
    {
        HttpListener _listener;
        Dispatcher dispatcher;
        ServerConfig serverConfig;
        public Listener(ServerConfig serverConfig)
        {
            this.serverConfig = serverConfig;
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://" + GetMyIP() + ":8080/");
            _listener.Prefixes.Add("http://localhost:8080/");
            _listener.Prefixes.Add("http://local1.com/");
            _listener.Prefixes.Add("http://local2.com/");
            dispatcher = new Dispatcher();
        }
        private static string GetMyIP()
        {
            string hostName = Dns.GetHostName();
            string IPv4 = Dns.GetHostEntry(hostName).AddressList[1].ToString();
            return IPv4;
        }
        public void Start()
        {
            _listener.Start();
            Console.WriteLine("Web Server Listening on http:" + _listener);
            Listen();
        }
        public void Listen()
        {
            HttpListenerContext context = _listener.GetContext();
            dispatcher.ServeRequest(context, serverConfig);
            Listen();
        }
    }
    public class Dispatcher
    {
        public void ServeRequest(HttpListenerContext context, ServerConfig serverConfig)
        {
            var app=serverConfig.GetApp(context.Request.Url.Host);
            app.HandleRequest(context);
        }
    }
    public class App
    {
        string name { get; set; }
        string directory { get; set; }
        HttpListenerRequest request;
        HttpListenerResponse response;
        public App(string name,string directory)
        {
            this.name = name;
            this.directory = directory;
            
        }
        public void HandleRequest(HttpListenerContext context)
        {
            request = context.Request;
            response = context.Response;
            string requestType = request.Url.Segments[1];
            if(requestType!="api/")
            {
                response= HandleStaticRequest();
            }
            else
            {
                response = HandleApiRequest();
            }
            response.OutputStream.Close();
        }

        public HttpListenerResponse HandleStaticRequest()
        {
            string filename = Path.Combine(directory, request.Url.Segments[1]);
            if (File.Exists(filename))
            {
                try
                {
                    Stream input = new FileStream(filename, FileMode.Open);

                    response.ContentType = "text/html";
                    response.ContentLength64 = input.Length;
                    response.AddHeader("Date", DateTime.Now.ToString("r"));
                    response.AddHeader("Last-Modified", File.GetLastWriteTime(filename).ToString("r"));

                    byte[] buffer = new byte[1024 * 32];
                    int nbytes;
                    while ((nbytes = input.Read(buffer, 0, buffer.Length)) > 0)
                        response.OutputStream.Write(buffer, 0, nbytes);
                    input.Close();
                    response.OutputStream.Flush();

                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                catch (Exception ex)
                {
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            return response;
        }
        private HttpListenerResponse HandleApiRequest()
        {
            if((request.Url.Segments[2]=="check-leap-year") && (request.HttpMethod=="POST"))
            {
                var data_text = new StreamReader(request.InputStream,
                                                 request.ContentEncoding)
                                                 .ReadToEnd();
//                Console.WriteLine(Newtonsoft.Json.JsonConvert.DeserializeObject(data_text));
                var json = System.Web.HttpUtility.UrlDecode(data_text);
                JObject requestBody = JObject.Parse(json);
                Console.WriteLine(requestBody);
                string year = (string)requestBody.SelectToken("year");

                if (year != null)
                {
                    string value = Int32.Parse(year) % 4 == 0 ? "true" : "false";
                    JObject responseJSON = JObject.Parse(@"{'IsLeap': '" + value + @"'}");
                    Console.WriteLine(responseJSON);
                    byte[] buf = Encoding.UTF8.GetBytes(responseJSON.ToString());
                    response.ContentLength64 = buf.Length;
                    response.ContentType = "application/json";
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.OutputStream.Write(buf, 0, buf.Length);
                }
                else
                {
                    byte[] buf = Encoding.UTF8.GetBytes("missing required data in request.");
                    response.ContentLength64 = buf.Length;
                    response.OutputStream.Write(buf, 0, buf.Length);
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            }
            else
            {
                byte[] buf = Encoding.UTF8.GetBytes("invalid request.");
                response.ContentLength64 = buf.Length;
                response.OutputStream.Write(buf, 0, buf.Length);
                response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            return response;
        }
    }
    public class ServerConfig
    {
        Dictionary<string, App> appDictionary;
        public ServerConfig()
        {
            appDictionary = new Dictionary<string, App>();
        }
        public void AddApp(string name,string directory)
        {
            appDictionary.Add(name, new App(name,directory));
        }
        public App GetApp(string name)
        {
            return appDictionary[name];
        }
    }
}
