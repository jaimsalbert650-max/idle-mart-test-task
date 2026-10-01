using IdleMart.Progression;
using NUnit.Framework;

namespace IdleMart.Tests
{
    public class StoreRatingTests
    {
        [Test]
        public void StartsAtDefault()
        {
            Assert.AreEqual(StoreRating.Default, new StoreRating().Value, 1e-5);
        }

        [Test]
        public void SatisfiedRaises_ComplaintLowersMore()
        {
            var rating = new StoreRating();
            rating.Satisfied();
            Assert.AreEqual(StoreRating.Default + StoreRating.SatisfiedStep, rating.Value, 1e-5);
            rating.Complained();
            Assert.AreEqual(StoreRating.Default + StoreRating.SatisfiedStep - StoreRating.ComplaintStep, rating.Value, 1e-5);
        }

        [Test]
        public void StaysWithinOneToFiveStars()
        {
            var rating = new StoreRating();
            for (var i = 0; i < 200; i++) rating.Satisfied();
            Assert.AreEqual(StoreRating.Max, rating.Value, 1e-5);
            for (var i = 0; i < 200; i++) rating.Complained();
            Assert.AreEqual(StoreRating.Min, rating.Value, 1e-5);
        }

        [TestCase(1f, 0.6f)]
        [TestCase(3f, 1.0f)]
        [TestCase(5f, 1.4f)]
        public void FlowMultiplier_ScalesWithStars(float stars, float expected)
        {
            var rating = new StoreRating();
            rating.Set(stars);
            Assert.AreEqual(expected, rating.FlowMultiplier, 1e-5);
        }

        [Test]
        public void Set_InvalidValue_FallsBackToDefault()
        {
            var rating = new StoreRating();
            rating.Set(-1f);
            Assert.AreEqual(StoreRating.Default, rating.Value, 1e-5);
        }
    }
}
