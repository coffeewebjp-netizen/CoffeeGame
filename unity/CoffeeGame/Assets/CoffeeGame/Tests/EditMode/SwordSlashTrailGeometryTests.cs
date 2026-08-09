using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Combat.Tests
{
    public sealed class SwordSlashTrailGeometryTests
    {
        [Test]
        public void Build_CreatesOpenDescendingCrescent()
        {
            Vector3[] points = SwordSlashTrailGeometry.Build(1f, false, 24);

            Assert.That(points, Has.Length.EqualTo(25));
            Assert.That(points[0].x, Is.LessThan(0f));
            Assert.That(points[0].y, Is.GreaterThan(0f));
            Assert.That(points[^1].x, Is.GreaterThan(0f));
            Assert.That(points[^1].y, Is.LessThan(0f));
            Assert.That(Vector3.Distance(points[0], points[^1]), Is.GreaterThan(1f));
            Assert.That(points, Has.All.Matches<Vector3>(point => Mathf.Approximately(point.z, 0f)));
        }

        [Test]
        public void Build_MirrorsOnlyHorizontalTrajectory()
        {
            Vector3[] right = SwordSlashTrailGeometry.Build(0.8f, false, 12);
            Vector3[] left = SwordSlashTrailGeometry.Build(0.8f, true, 12);

            Assert.That(left, Has.Length.EqualTo(right.Length));
            for (int index = 0; index < right.Length; index++)
            {
                Assert.That(left[index].x, Is.EqualTo(-right[index].x).Within(0.0001f));
                Assert.That(left[index].y, Is.EqualTo(right[index].y).Within(0.0001f));
            }
        }

        [Test]
        public void Build_RejectsNonPositiveRadius()
        {
            Assert.That(SwordSlashTrailGeometry.Build(0f, false), Is.Empty);
        }
    }
}
