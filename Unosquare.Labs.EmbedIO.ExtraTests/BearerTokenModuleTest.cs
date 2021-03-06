﻿namespace Unosquare.Labs.EmbedIO.ExtraTests
{
    using Newtonsoft.Json;
    using NUnit.Framework;
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using Unosquare.Labs.EmbedIO.BearerToken;
    using Unosquare.Labs.EmbedIO.ExtraTests.Properties;
    using Unosquare.Labs.EmbedIO.Markdown;

    public class BearerTokenModuleTest
    {
        protected string RootPath;
        protected BasicAuthorizationServerProvider BasicProvider = new BasicAuthorizationServerProvider();
        protected WebServer WebServer;
        protected TestConsoleLog Logger = new TestConsoleLog();

        [SetUp]
        public void Init()
        {
            RootPath = TestHelper.SetupStaticFolder();

            WebServer = new WebServer(Resources.ServerAddress, Logger);
            WebServer.RegisterModule(new BearerTokenModule(BasicProvider));
            WebServer.RegisterModule(new MarkdownStaticModule(RootPath));
            WebServer.RunAsync();
        }

        [Test]
        public void TestBasicAuthorizationServerProvider()
        {
            try
            {
                BasicProvider.ValidateClientAuthentication(new ValidateClientAuthenticationContext(null))
                    .RunSynchronously();
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.GetType(), typeof (ArgumentNullException));
            }
        }

        [Test]
        public void GetInvalidToken()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes("grant_type=nothing");

            var request = (HttpWebRequest) WebRequest.Create(Resources.ServerAddress + "token");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = payload.Length;

            using (var dataStream = request.GetRequestStream())
            {
                dataStream.Write(payload, 0, payload.Length);
            }

            try
            {
                var response = (HttpWebResponse) request.GetResponse();
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ex.Status != WebExceptionStatus.ProtocolError)
                    throw;

                var response = (HttpWebResponse) ex.Response;

                Assert.AreEqual(response.StatusCode, HttpStatusCode.Unauthorized, "Status Code Unauthorized");

            }
        }

        [Test]
        public void GetValidToken()
        {
            var token = string.Empty;
            var payload = System.Text.Encoding.UTF8.GetBytes("grant_type=password&username=test&password=test");

            var request = (HttpWebRequest) WebRequest.Create(Resources.ServerAddress + "token");
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = payload.Length;

            using (var dataStream = request.GetRequestStream())
            {
                dataStream.Write(payload, 0, payload.Length);
            }

            using (var response = (HttpWebResponse) request.GetResponse())
            {
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");

                var jsonString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                Assert.IsNotNullOrEmpty(jsonString);

                var json = JsonConvert.DeserializeObject<BearerToken>(jsonString);
                Assert.IsNotNull(json);
                Assert.IsNotNullOrEmpty(json.Token);
                Assert.IsNotNullOrEmpty(json.Username);

                token = json.Token;
            }

            var indexRequest = (HttpWebRequest) WebRequest.Create(Resources.ServerAddress + "index.html");

            try
            {
                using (var response = (HttpWebResponse) indexRequest.GetResponse())
                {
                    Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ex.Status != WebExceptionStatus.ProtocolError)
                    throw;

                var response = (HttpWebResponse) ex.Response;

                Assert.AreEqual(response.StatusCode, HttpStatusCode.Unauthorized, "Status Code Unauthorized");

            }

            indexRequest = (HttpWebRequest) WebRequest.Create(Resources.ServerAddress + "index.html");
            indexRequest.Headers.Add("Authorization", "Bearer " + token);

            using (var response = (HttpWebResponse) indexRequest.GetResponse())
            {
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");
            }
        }

        [TearDown]
        public void Kill()
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
            WebServer.Dispose();
        }
    }
}