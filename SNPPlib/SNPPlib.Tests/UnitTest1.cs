using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SNPPlib;

namespace SNPPlib.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var client = new Client("snpp.amsmsg.net", 444);

            var connected = await client.Connect();
            if (connected)
            {
                var resp = await client.Help();
                //await client.Pager("5551234");
                //var resp = await client.TwoWay();

                //var time = DateTime.Now.AddDays(3);
                //var resp = await client.HoldUntil(time, TimeZone.CurrentTimeZone.GetUtcOffset(time));

                await client.Quit();
            }


            //var resp1 = new Response("421 Gateway Service Unavailable\r\n");
            //var resp2 = new Response("999 Gateway Service Unavailable\r\n");
            //var resp3 = new Response("abc Gateway Service Unavailable\r\n");

            //var mal = resp3.Equals(ResponseCode.Malformed);

            var a = 3;
        }
    }
}
