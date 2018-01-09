using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace api.Controllers
{
    [Route("api/[controller]")]
    public class CryptoController : Controller
    {
        static HttpClient http = new HttpClient();

        static IMongoDatabase database = null;

        static IMongoCollection<BalanceItem> cryptocollection = null;

        static IMongoCollection<Config> configcollection = null;

        static CryptoController()
        {
            database = new MongoClient().GetDatabase("mining");
            cryptocollection = database.GetCollection<BalanceItem>("crypto");
            configcollection = database.GetCollection<Config>("config");

            var conf = configcollection.Find(Builders<Config>.Filter.Empty);

            if (!conf.Any())
            {
                configcollection.InsertOne(cryptoconfig);
            }
            else
            {
                cryptoconfig = conf.First();
            }
        }

        [HttpGet]
        [Route("balance/{symbol}")]
        public async Task<double> getBalance(string symbol)
        {
            var filter = Builders<BalanceItem>.Filter.Eq("_id", symbol);

            var api = cryptoconfig.config.Where(d => d.Symbol == symbol).FirstOrDefault();

            if(api == null)return 0;

            if (symbol == "MIOTA") return double.Parse(api.WebApi);

            try
            {
                HttpResponseMessage request = await http.GetAsync(string.Format(api.WebApi, string.Join(api.Delimitor, api.Addresses)));
                request.EnsureSuccessStatusCode();

                JObject json = (JObject)JObject.Parse(await request.Content.ReadAsStringAsync());
                IEnumerable<JToken> selected = json.SelectTokens(api.select);
                var value = selected.Select(x => (string)x).Sum(x => double.Parse(x)) / Math.Pow(10, api.DivPow);

                await cryptocollection.ReplaceOneAsync(filter, new BalanceItem { Symbol = symbol, Balance = value }, new UpdateOptions() { IsUpsert = true });

                return value;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);
            }

            var item = await database.GetCollection<BalanceItem>("crypto").FindAsync(filter);
            var first = item.First();
            return first.Balance;
        }

        [HttpGet]
        [Route("balance")]
        public async Task<dynamic> getAllBalances()
        {
            var items = new List<BalanceItem>();

            foreach (ConfigItem ci in cryptoconfig.config)
            {
                items.Add(new BalanceItem { Symbol = ci.Symbol, Balance = await getBalance(ci.Symbol) });
            }

            return items;
        }


        // GET api/values
        [HttpGet]
        public async Task<dynamic> Get()
        {
            return new { config = cryptoconfig, balances = await getAllBalances() };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public async Task<dynamic> Post([FromBody]Config value)
        {
            cryptoconfig = value;
            return await configcollection.ReplaceOneAsync(Builders<Config>.Filter.Eq("_id", "crypto_config"), value);
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        public class Config
        {
            public string _id { get; set; }

            public ConfigItem[] config { get; set; }
        }

        public class ConfigItem
        {
            public string Symbol { get; set; }
            public string WebApi { get; set; }
            public string[] Addresses { get; set; }
            public string Delimitor { get; set; }
            public string select { get; set; }
            public int DivPow { get; set; }
        }

        static Config cryptoconfig = new Config
        {
            _id = "crypto_config",
            config = new ConfigItem[]
            {
                new ConfigItem
                {
                    Symbol = "BTC",
                    WebApi = "https://blockchain.info/multiaddr?active={0}&cors=true",
                    Addresses = new [] {
                        "3FjevoW6ASivzPLfwrfYc7aFr66vADm73X",
                        "15w5pH2GppzM9niS3tBYoWDskRpvCVQJxr"
                    },
                    Delimitor = "|",
                    select = "$.wallet.final_balance",
                    DivPow = 8
                },
                new ConfigItem
                {
                    Symbol = "ETH",
                    WebApi = "https://api.etherscan.io/api?module=account&action=balancemulti&address={0}&tag=latest&apikey=UJECKJSEWHV7HZF48J692T7QQ4NR7JICMR",
                    Addresses = new [] {
                        "0xC6688e5b1744eD0c6Dc76209e68A6eB54ff7f2b1",
                        "0x3988FD7E58C576346e9A0Ea7Ae376aFbE3D28338",
                        "0xF6320aA229557ef349e3F6d825E03BFB2095Ae8C",
                        "0x4Bb579879b49C9c8bacFda61dbc0E1F506f21D88",
                        "0xB662a3dFc9270e6452d7A48B9C63B2c8dC20F2c8"
                    },
                    Delimitor = ",",
                    select = "$.result[*].balance",
                    DivPow = 18
                },
                new ConfigItem
                {
                    Symbol = "BCH",
                    WebApi = "https://bitcoincash.blockexplorer.com/api/addr/{0}/",
                    Addresses = new [] {
                        "1KEvcvYFCwVXhH9nMx3eHUURbRicdV7Tna"

                    },
                    select = "$.balanceSat",
                    DivPow = 8
                },
                new ConfigItem
                {
                    Symbol = "LTC",
                    WebApi = "https://api.blockcypher.com/v1/ltc/main/addrs/{0}",
                    Addresses = new [] {
                        "3D5UFBhuhT8EVnn6jrdjERaEvBbx3rTHbq"
                    },
                    select = "$.final_balance",
                    DivPow = 8
                },
                new ConfigItem
                {
                    Symbol = "MIOTA",
                    WebApi = "41.98",
                    DivPow = 8
                }
            }
        };
    }
}
