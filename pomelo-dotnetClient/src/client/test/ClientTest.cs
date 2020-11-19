using System;
using SimpleJson;

namespace Pomelo.DotNetClient.Test
{
    public class ClientTest
    {
        public static PomeloClient pc = null;

        public static void loginTest(string host, int port)
        {
            pc = new PomeloClient();

            pc.NetWorkStateChangedEvent += (state) =>
            {
                Console.WriteLine(state);
            };


            pc.initClient(host, port, () =>
            {
                pc.connect(null, data =>
                {

                    Console.WriteLine("on data back" + data.ToString());
                    JsonObject msg = new JsonObject();
                    msg["uid"] = 111;
                    pc.request("auth.authHandler.test", msg, OnQuery);
                });
            });

            pc.on("TEST_PUSH", (result) =>
            {
                Console.WriteLine(result.ToString());
            });
        }

        public static void OnQuery(JsonObject result)
        {
            Console.WriteLine("on data back2" + result.ToString());
            if (Convert.ToInt32(result["code"]) == 0)
            {
                //JsonObject msg = new JsonObject();
                //msg["uid"] = 111;
                //msg["token"] = "123";
                //pc.request("connector.loginHandler.login", msg, (data) =>
                //{
                //    Console.WriteLine("on data back3" + result.ToString());
                //});

                JsonObject msg = new JsonObject();
                msg["clientData"] = "testpush";
                pc.request("test.authHandler.testPush", msg, (data) =>
                {
                    Console.WriteLine("on data back3" + result.ToString());
                });
            }
        }

        public static void OnEnter(JsonObject result)
        {
            Console.WriteLine("on login " + result.ToString());
        }

        public static void onDisconnect(JsonObject result)
        {
            Console.WriteLine("on sockect disconnected!");
        }

        public static void Run()
        {
            string host = "8.129.45.102";
            int port = 7002;

            loginTest(host, port);
        }
    }
}