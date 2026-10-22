using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Divinity.Surfaces.Tests
{
    public class SurfaceSimulationTests
    {
        const float TickInterval = 0.1f;

        World _world;
        SystemHandle _simulation;
        double _elapsed;

        [SetUp]
        public void SetUp()
        {
            _world = new World("SurfaceSimulationTests");
            _elapsed = 0;

            var config = new SurfaceGridConfig
            {
                Size = new int2(6, 3),
                CellSize = 1f,
                TickInterval = TickInterval,
                FireDuration = 3f,
                ElectricDuration = 0.25f,
                SteamDuration = 4f,
                Seed = 1,
            };
            var grid = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(grid, config);
            _world.EntityManager.AddComponentData(grid, new SurfaceSimulationTimer { TimeUntilTick = TickInterval });

            _world.CreateSystem<SurfaceGridSpawnSystem>().Update(_world.Unmanaged);
            _simulation = _world.CreateSystem<SurfaceSimulationSystem>();

            for (int i = 0; i < config.Size.x * config.Size.y; i++)
                Set(i, SurfaceType.None);
        }

        [TearDown]
        public void TearDown()
        {
            _world.Dispose();
        }

        [Test]
        public void Fire_SpreadsOverOil_OneCellPerTick()
        {
            Set(0, SurfaceType.Fire);
            for (int i = 1; i < 6; i++)
                Set(i, SurfaceType.Oil);

            Tick(1);
            Assert.AreEqual(SurfaceType.Fire, Get(1));
            Assert.AreEqual(SurfaceType.Oil, Get(2));

            Tick(2);
            Assert.AreEqual(SurfaceType.Fire, Get(3));
            Assert.AreEqual(SurfaceType.Oil, Get(4));
        }

        [Test]
        public void Fire_SpreadsToFourNeighbours_NotDiagonally()
        {
            Set(Index(2, 1), SurfaceType.Fire);
            Set(Index(1, 1), SurfaceType.Oil);
            Set(Index(3, 1), SurfaceType.Oil);
            Set(Index(2, 0), SurfaceType.Oil);
            Set(Index(2, 2), SurfaceType.Oil);
            Set(Index(1, 0), SurfaceType.Oil);

            Tick(1);

            Assert.AreEqual(SurfaceType.Fire, Get(Index(1, 1)), "left");
            Assert.AreEqual(SurfaceType.Fire, Get(Index(3, 1)), "right");
            Assert.AreEqual(SurfaceType.Fire, Get(Index(2, 0)), "down");
            Assert.AreEqual(SurfaceType.Fire, Get(Index(2, 2)), "up");
            Assert.AreEqual(SurfaceType.Oil, Get(Index(1, 0)), "diagonal");
        }

        [Test]
        public void Fire_DoesNotJumpOverEmptyCell()
        {
            Set(0, SurfaceType.Fire);
            Set(2, SurfaceType.Oil);

            Tick(5);

            Assert.AreEqual(SurfaceType.Oil, Get(2));
        }

        [Test]
        public void Fire_NextToWater_MakesSteam()
        {
            Set(0, SurfaceType.Fire);
            Set(1, SurfaceType.Water);

            Tick(1);

            Assert.AreEqual(SurfaceType.Steam, Get(1));
        }

        [Test]
        public void Fire_BurnsOut()
        {
            Set(0, SurfaceType.Fire);

            Tick(29);
            Assert.AreEqual(SurfaceType.Fire, Get(0));

            Tick(2);
            Assert.AreEqual(SurfaceType.None, Get(0));
        }

        [Test]
        public void Electricity_SpreadsOverWater_AndTurnsBackIntoWater()
        {
            Set(0, SurfaceType.ElectrifiedWater);
            Set(1, SurfaceType.Water);
            Set(2, SurfaceType.Water);
            Set(3, SurfaceType.Oil);

            Tick(2);
            Assert.AreEqual(SurfaceType.ElectrifiedWater, Get(2));
            Assert.AreEqual(SurfaceType.Oil, Get(3));

            Tick(10);
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(SurfaceType.Water, Get(i));
        }

        void Tick(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _elapsed += TickInterval;
                _world.SetTime(new TimeData(_elapsed, TickInterval));
                _simulation.Update(_world.Unmanaged);
                _world.EntityManager.CompleteAllTrackedJobs();
            }
        }

        int Index(int x, int y)
        {
            return y * Config().Size.x + x;
        }

        Entity Cell(int index)
        {
            var query = _world.EntityManager.CreateEntityQuery(typeof(SurfaceCellRef));
            var cells = query.GetSingletonBuffer<SurfaceCellRef>(isReadOnly: true);
            return cells[index].Value;
        }

        void Set(int index, SurfaceType type)
        {
            _world.EntityManager.SetComponentData(Cell(index), SurfaceRules.Make(type, Config()));
        }

        SurfaceType Get(int index)
        {
            return _world.EntityManager.GetComponentData<Surface>(Cell(index)).Type;
        }

        SurfaceGridConfig Config()
        {
            var query = _world.EntityManager.CreateEntityQuery(typeof(SurfaceGridConfig));
            return query.GetSingleton<SurfaceGridConfig>();
        }
    }
}
