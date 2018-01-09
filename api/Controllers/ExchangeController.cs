using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace api.Controllers
{
    [Route("api/[controller]")]
    public class ExchangeController : Controller
    {
        static HttpClient http = new HttpClient();

        static IMongoDatabase database = null;

        static IMongoCollection<Ticker> exchangeCollection = null;

        static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        static Ticker ticker = null;

        static ExchangeController()
        {
            http.DefaultRequestHeaders.ExpectContinue = false;
            database = new MongoClient().GetDatabase("mining");
            exchangeCollection = database.GetCollection<Ticker>("exchange");

            Action start = async () => await StartTicker(TimeSpan.FromSeconds(60), cancellationTokenSource.Token);

            Task.Run(start).ConfigureAwait(false);/*.ContinueWith(x => {
                    x.Exception.Handle(ex =>
                    {
                        Console.WriteLine(ex.Message);
                        return true;
                    });
              }, TaskContinuationOptions.OnlyOnFaulted);//await StartTicker(TimeSpan.FromSeconds(5), cancellationTokenSource.Token);*/
        }

        [HttpGet]
        public async Task<Ticker> Get()
        {
            return ticker;
        }

        [HttpGet]
        [Route("{symbol}/{balance}")]
        public async Task<TickerData> GetMoney(string symbol, double balance)
        {
            
            var data = (await Get())?.data.SingleOrDefault(x=>x.Symbol == symbol);

            if(data==null)return null;

            return new TickerData { Symbol = symbol, Price_Eur = data.Price_Eur * balance, Price_Usd = data.Price_Usd * balance };

        }

        static async Task StartTicker(TimeSpan interval, CancellationToken token)
        {
            while (true)
            {
                try
                {
                    await FetchTickerData();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    await Task.Delay(interval, token);
                }
            }
        }

        static async Task FetchTickerData()
        {
            HttpResponseMessage response = await http.GetAsync("https://api.coinmarketcap.com/v1/ticker/?convert=EUR");
            response.EnsureSuccessStatusCode();

            var json = JArray.Parse(await response.Content.ReadAsStringAsync());

            ticker = new Ticker { _id="ticker", data = json.ToObject<TickerData[]>() };

            await exchangeCollection.ReplaceOneAsync(Builders<Ticker>.Filter.Eq("_id", "ticker"), ticker, new UpdateOptions() { IsUpsert = true });
        }

        public class Ticker {
            public string _id {get;set;}

            public TickerData[] data {get;set;}
        }
        public class TickerData
        {
            public string Symbol { get; set; }

            public double Price_Usd { get; set; }

            public double Price_Eur {get;set;}
        }
    }
}