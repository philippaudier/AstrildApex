using System;
using Engine.Mathx.Noise;
using NUnit.Framework;

namespace Tests.Mathx
{
    public class PerlinNoiseTests
    {
        [Test]
        public void SampleRepeatableWithSeed()
        {
            var a = new PerlinNoise(123);
            var b = new PerlinNoise(123);
            float v1 = a.Sample(10.5f, -3.2f);
            float v2 = b.Sample(10.5f, -3.2f);
            Assert.AreEqual(v1, v2);
        }
    }
}
