﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace V7mBot.AI
{
    public class Knowledge
    {
        private class NavQuery
        {
            public NavGrid Grid;
            public NavGrid.CostQuery SeedFunction;
            public NavGrid.CostQuery CostFunction;
        }

        private Dictionary<string, NavQuery> _charts = new Dictionary<string, NavQuery>();       
        private GameResponse _rawData;
        private HeroInfo _hero;
        private List<HeroInfo> _heroes;
        private TileMap _map;

        public HeroInfo Hero
        {
            get
            {
                return _hero;
            }
        }

        public IEnumerable<HeroInfo> Heroes
        {
            get
            {
                return _heroes;
            }
        }

        public TileMap Map
        {
            get
            {
                return _map;
            }
        }
        public GameResponse RawData
        {
            get
            {
                return _rawData;
            }
        }

        public NavGrid this[string chartName]
        {
            get { return _charts[chartName].Grid; }
        }

        public Knowledge(GameResponse rawData)
        {
            _rawData = rawData;

            int mapSize = _rawData.game.board.size;

            _map = new TileMap(mapSize);
            _map.Parse(_rawData.game.board.tiles);

            int index = 0;
            _heroes = _rawData.game.heroes.Select(data => new HeroInfo(this, index++)).ToList();
            _hero = _heroes.First(h => h.ID == _rawData.hero.id);
        }

        public void Update(GameResponse rawData)
        {
            _rawData = rawData;
            _map.Parse(rawData.game.board.tiles);
            //TODO: --> stuff like this needs to be bot specific
            foreach(var q in _charts.Values)
            {
                UpdateChart(q.Grid, q.SeedFunction, q.CostFunction);
            }
        }
        
        private bool IsPassable(TileMap.Tile tile)
        {
            if (tile.Type == TileMap.TileType.Free)
                return true;
            if (tile.Type == TileMap.TileType.Hero)
                return true;
            return false;
        }
        
        private void Chart(string name, NavGrid.CostQuery seedFunc, NavGrid.CostQuery costFunc)
        {
            _charts[name] = new NavQuery()
            {
                Grid = new NavGrid(Map.Width, Map.Height),
                CostFunction = costFunc,
                SeedFunction = seedFunc
            };
        }

        public void Chart(string name, Predicate<TileMap.Tile> seedFilter, NavGrid.CostQuery costFunc)
        {
            Chart(name, (x, y) => seedFilter(_map[x, y]) ? 0 : -1, costFunc);
        }

        public void Chart(string name, Predicate<TileMap.Tile> seedFilter, Func<TileMap.Tile, float> costFunc)
        {
            Chart(name, (x, y) => seedFilter(_map[x, y]) ? 0 : -1, (x, y) => costFunc(_map[x, y]));
        }

        public void Chart(string name, Predicate<TileMap.Tile> seedFilter, Func<TileMap, int, int, float> costFunc)
        {
            Chart(name, (x, y) => seedFilter(_map[x, y]) ? 0 : -1, (x, y) => costFunc(_map, x, y));
        }

        public void Chart(string name, Func<TileMap, int, int, float> seedCost, Func<TileMap, int, int, float> costFunc)
        {
            Chart(name, (x, y) => seedCost(_map, x, y), (x, y) => costFunc(_map, x, y));
        }

        public void Chart(string name, Func<TileMap, int, int, float> seedCost, NavGrid.CostQuery costFunc)
        {
            Chart(name, (x, y) => seedCost(_map, x, y), costFunc);
        }

        private void UpdateChart(NavGrid nav, NavGrid.CostQuery seeds, NavGrid.CostQuery costs)
        {
            nav.Reset();
            if(costs != null)
            {
                nav.SetSeeds(seeds);
                nav.SetCosts(costs);
            }
            nav.Flood();
        }
        
        //PREDICATES

        public Predicate<TileMap.Tile> TypeFilter(TileMap.TileType typeMask)
        {
            return tile => (tile.Type & typeMask) > 0;
        }

        public Predicate<TileMap.Tile> TypeFilter(TileMap.TileType typeMask, int heroID)
        {
            return tile => (tile.Type & typeMask) > 0 && tile.Owner != heroID;
        }


        //COST MODIFIER

        public NavGrid.CostQuery CostByChart(string chart, float zeroValue, float scale)
        {
            return (x, y) =>
            {
                var node = _map[x, y];
                int id = _hero.ID;
                if (node.Type == TileMap.TileType.Free || (node.Type == TileMap.TileType.Hero && node.Owner == id))
                    return 1 + SampleChartNormalized(x, y, chart, zeroValue) * scale;
                else
                    return -1;
            };
        }

        public float SampleChartNormalized(int x, int y, string chart, float zeroValue)
        {
            if (zeroValue == 0)
                return 0;
            return Math.Max(0, zeroValue - this[chart][x, y].PathCost) / zeroValue;
        }

        public float DefaultCost(TileMap.Tile node)
        {
            int id = _hero.ID;
            if (node.Type == TileMap.TileType.Free || (node.Type == TileMap.TileType.Hero && node.Owner == id))
                return 1;
            else
                return -1;
        }
    }
}
