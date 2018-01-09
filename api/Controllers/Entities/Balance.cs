using MongoDB.Bson.Serialization.Attributes;

namespace api.Controllers
{
    class BalanceItem
    {
        [BsonId]
        public string Symbol { get; set; }

        public double Balance { get; set; }
    }
}