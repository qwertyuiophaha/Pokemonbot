﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AllEnum;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Helpers;
using System.Timers;

namespace PokemonGo.RocketAPI.Console
{
    class Program
    {
        static int myMaxPokemon = 250;
        static int basePokemonCount = 250;        
        static string password = "Poke1234";

        static void Main(string[] args)
        {
            if (args != null && args.Count() > 0)
            {
                try
                {
                    Settings.AuthType = AuthType.Ptc;
                    Settings.PtcUsername = args[0];
                    Settings.PtcPassword = "Poke1234";
                    if (args.Count() > 1)
                    {
                        if(args[0].Trim().ToLowerInvariant() == "google")
                        {
                            if (args[1].Trim().ToLowerInvariant() == "dratini")
                            {
                                Settings.AuthType = AuthType.Google;
                                Settings.PtcUsername = args[2];
                            }
                            else
                            {
                                Settings.AuthType = AuthType.Google;
                                Settings.PtcUsername = args[1];
                            }

                        }
                        else if(args[0].Trim().ToLowerInvariant() == "dratini")
                        {         
                            Settings.PtcUsername = args[1];
                            Settings.DratiniMode = true;
                        }
                        else
                        {
                            Settings.PtcPassword = args[1];
                        }
                        if(args.Count() == 4)
                        {
                            Settings.DefaultLongitude = long.Parse(args[3]);
                            Settings.DefaultLatitude = long.Parse(args[2]);
                            System.Console.WriteLine($"Location Set to Lat: {Settings.DefaultLatitude} Long: {Settings.DefaultLongitude}");
                        }
                        else
                        {
                            System.Console.WriteLine($"Location Set to New York");
                        }                        
                    }
                }
                catch (Exception e)
                {
                    System.Console.WriteLine($"Error in Command Line input");
                    UserInput();
                }
            }
            else
            {
                UserInput();
            }
            Task.Run(() => Execute());
            System.Console.ReadLine();
        }

        static void UserInput()
        {
            System.Console.WriteLine($"Location Set to New York Central Park by Default");
            System.Console.WriteLine($"Input Longitude, press enter if going with New York, Enter 1 to Santa Monica, Enter 2 to San Fran, Enter 3 to Sydney:");
            var longi = System.Console.ReadLine();
            if (!string.IsNullOrEmpty(longi))
            {
                if(longi.Trim() == "1")
                {
                    Settings.DefaultLatitude = Settings.SantaMonicaLatitude;
                    Settings.DefaultLongitude = Settings.SantaMonicaLongitude;
                }
                else if(longi.Trim() == "3")
                {
                    Settings.DefaultLatitude = Settings.SidneyLatitude;
                    Settings.DefaultLongitude = Settings.SidneyLongitude;
                }
                else if (longi.Trim() == "2")
                {
                    Settings.DefaultLatitude = Settings.SanFranLatitude;
                    Settings.DefaultLongitude = Settings.SanFranLongitude;
                }
                else
                {
                    System.Console.WriteLine($"Input Latitude:");
                    Settings.DefaultLatitude = Double.Parse(System.Console.ReadLine());
                    Settings.DefaultLongitude = Double.Parse(longi);
                }                
            }

            System.Console.WriteLine($"Using PTC? (y/n)?");
            var ptc = System.Console.ReadLine();

            if (ptc == "y" || Settings.AuthType == AuthType.Ptc)
            {
                Settings.AuthType = AuthType.Ptc;
                System.Console.WriteLine($"PTC Username:");
                Settings.PtcUsername = System.Console.ReadLine();
                System.Console.WriteLine($"PTC Password:");
                Settings.PtcPassword = System.Console.ReadLine();
            }
        }

        static async void Execute()
        {
            ReExecute:
            try
            {
                if(Settings.DratiniMode)
                {
                    Settings.DefaultLatitude = Settings.DratiniLatitude;
                    Settings.DefaultLongitude = Settings.DratiniLongitude;
                    System.Console.WriteLine("Starting Dratini Farm");
                }
                var client = new Client(Settings.DefaultLatitude, Settings.DefaultLongitude);

                if (Settings.AuthType == AuthType.Ptc)
                {
                    await client.DoPtcLogin(Settings.PtcUsername, Settings.PtcPassword);
                }
                else if (Settings.AuthType == AuthType.Google)
                    Settings.GoogleRefreshToken = await client.DoGoogleLogin(Settings.GoogleRefreshToken);

                await client.SetServer();

                var profile = await client.GetProfile();
                var settings = await client.GetSettings();
                var mapObjects = await client.GetMapObjects();
                var inventory = await client.GetInventory();

                //await Client.TransferAllButStrongestUnwantedPokemon(client);

                while (true)
                {
                    if(Settings.DratiniMode)
                    {
                        var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null);
                        var pokeUpgrades = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.InventoryUpgrades).Where(p => p != null).SelectMany(x => x.InventoryUpgrades_).Where(x => x.UpgradeType == InventoryUpgradeType.IncreasePokemonStorage).Count();
                        myMaxPokemon = basePokemonCount + pokeUpgrades * 50;

                        System.Console.WriteLine($"PokemonCount:" + pokemons.Count() + " Out of " + myMaxPokemon);


                        if (pokemons.Count() >= myMaxPokemon - 20)
                        {
                            await Client.TransferAllButStrongestUnwantedPokemon(client);
                        }

                        await ExecuteFarmingDratinis(client);
                    }
                    else
                    {
                        await ExecuteFarmingPokestopsAndPokemons(client);
                    }
                    
                    System.Console.WriteLine("Resetting Player Position");
                    var update = await client.UpdatePlayerLocation(Settings.DefaultLatitude, Settings.DefaultLongitude);
                    await RecycleItems(client);
                    if(Settings.DratiniMode)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        await Task.Delay(15000);
                    }                    
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine($"Exception occurred, Restarting..");
                await Task.Delay(10000);
                goto ReExecute;
            }
        }

        private static async Task ExecuteFarmingDratinis(Client client)
        {
            var mapObjects = await client.GetMapObjects();
            
            foreach (var coord in Common.Coordinates)
            {
                var update = await client.UpdatePlayerLocation(coord.Item1, coord.Item2);
               
                await ExecuteCatchAllNearbyPokemons(client);
            }
        }

        private static async Task ExecuteFarmingPokestopsAndPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            var pokeStops = mapObjects.MapCells.SelectMany(i => i.Forts).Where(i => i.Type == FortType.Checkpoint && i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime());
            pokeStops = SortRoute(pokeStops.ToList());

            int waitCount = 0;
            Timer timer = new Timer();

            foreach (var pokeStop in pokeStops)
            {
                var update = await client.UpdatePlayerLocation(pokeStop.Latitude, pokeStop.Longitude);
                var fortInfo = await client.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                var fortSearch = await client.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);

                if (fortSearch != null)
                {
                    System.Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] Farmed XP: {fortSearch.ExperienceAwarded}, Gems: { fortSearch.GemsAwarded}, Eggs: {fortSearch.PokemonDataEgg} Items: {GetFriendlyItemsString(fortSearch.ItemsAwarded)}");
                    var inventory = await client.GetInventory();
                    var settings = await client.GetSettings();
                    if (inventory != null && inventory.InventoryDelta != null)
                    {
                        var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null);
                        var playerData = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PlayerStats).Where(p => p != null && p?.Level > 0);
                        var pData = playerData.FirstOrDefault();
                        var pokeUpgrades = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.InventoryUpgrades).Where(p => p != null).SelectMany(x => x.InventoryUpgrades_).Where(x => x.UpgradeType == InventoryUpgradeType.IncreasePokemonStorage).Count();
                        myMaxPokemon = basePokemonCount + pokeUpgrades * 50;

                        System.Console.WriteLine($"PokemonCount:" + pokemons.Count() + " Out of " + myMaxPokemon);

                        if (pData != null)
                        {
                            System.Console.Title = string.Format("{0} level {1:0} - ({2:0} / {3:0})",
                              Settings.PtcUsername,+pData.Level,
                             +(pData.Experience - pData.PrevLevelXp),
                              +(pData.NextLevelXp - pData.PrevLevelXp));
                        }


                        if (pokemons.Count() >= myMaxPokemon-20)
                        {
                            await Client.TransferAllButStrongestUnwantedPokemon(client);
                        }

                        await ExecuteCatchAllNearbyPokemons(client);

                        if (fortSearch.ExperienceAwarded == 0)
                        {
                            waitCount++;
                        }
                        if (waitCount > 3)
                        {
                            System.Console.WriteLine("Waiting for 30 secs for soft ban");
                            await Task.Delay(30000);
                            waitCount = 0;
                        }
                    }
                }

                await Task.Delay(8000);
            }
        }

        private static async Task ExecuteCatchAllNearbyPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            var pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons );

            if(Settings.DratiniMode)
            {
                pokemons = pokemons.Where(x => x.PokemonId == PokemonId.Dratini || x.PokemonId == PokemonId.Dragonair || x.PokemonId == PokemonId.Dragonite);
            }

            foreach (var pokemon in pokemons)
            {
                var update = await client.UpdatePlayerLocation(pokemon.Latitude, pokemon.Longitude);
                var encounterPokemonRespone = await client.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnpointId);

                CatchPokemonResponse caughtPokemonResponse;
        
                var berryUsed = false;
                var inventoryBalls = await client.GetInventory();
                var items = inventoryBalls.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData.Item).Where(p => p != null);
                if(encounterPokemonRespone.WildPokemon != null && !(Common.PokemonIgnorelist.Contains(pokemon.PokemonId)))
                {
                    var pokeBall = await GetBestBall(encounterPokemonRespone?.WildPokemon, inventoryBalls);
                    do
                    {
                        if (!berryUsed && encounterPokemonRespone.CaptureProbability.CaptureProbability_.First() < 0.4 && Common.BerryPokemons.Contains(pokemon.PokemonId) && items.Where(p => p.Item_ == ItemId.ItemRazzBerry).Count() > 0 && items.Where(p => p.Item_ == ItemId.ItemRazzBerry).First().Count > 0)
                        {
                            berryUsed = true;
                            System.Console.Write($"Use Rasperry (" + items.Where(p => p.Item_ == ItemId.ItemRazzBerry).First().Count + ")!");
                            UseItemCaptureRequest useRaspberry = await client.UseCaptureItem(pokemon.EncounterId, AllEnum.ItemId.ItemRazzBerry, pokemon.SpawnpointId);
                            await Task.Delay(3000);
                        }
                        caughtPokemonResponse = await client.CatchPokemon(pokemon.EncounterId, pokemon.SpawnpointId, pokemon.Latitude, pokemon.Longitude, pokeBall);
                    } while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape);

                    System.Console.WriteLine(caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess ? $"[{DateTime.Now.ToString("HH:mm:ss")}] We caught a {pokemon.PokemonId} with CP {encounterPokemonRespone?.WildPokemon?.PokemonData?.Cp}" : $"[{DateTime.Now.ToString("HH:mm:ss")}] {pokemon.PokemonId} got away.. with CP {encounterPokemonRespone?.WildPokemon?.PokemonData?.Cp}");
                }
                if(Settings.DratiniMode)
                {
                    var families = await GetPokemonFamilies(inventoryBalls);
                    System.Console.Title = string.Format($"{Settings.PtcUsername} Dratini Candy: {families.First(x => x.FamilyId == PokemonFamilyId.FamilyDratini)?.Candy}");
                }

                await Task.Delay(5000);
            }
        }

        private static async Task<List<PokemonFamily>> GetPokemonFamilies(GetInventoryResponse inventory)
        {
            var families = from item in inventory.InventoryDelta.InventoryItems
                           where item.InventoryItemData?.PokemonFamily != null
                           where item.InventoryItemData?.PokemonFamily.FamilyId != PokemonFamilyId.FamilyUnset
                           group item by item.InventoryItemData?.PokemonFamily.FamilyId into family
                           select new PokemonFamily
                           {
                               FamilyId = family.First().InventoryItemData.PokemonFamily.FamilyId,
                               Candy = family.First().InventoryItemData.PokemonFamily.Candy
                           };


            return families.ToList();
        }

        private static async Task<MiscEnums.Item> GetBestBall(WildPokemon pokemon, GetInventoryResponse inventory)
        {
            var pokemonCp = pokemon?.PokemonData?.Cp;
            var pokeBallsCount = inventory.InventoryDelta.InventoryItems.Select(x => x.InventoryItemData?.Item).Where(x => x != null && x.Item_ == ItemId.ItemPokeBall).FirstOrDefault()?.Count ?? 0;
            var greatBallsCount = inventory.InventoryDelta.InventoryItems.Select(x => x.InventoryItemData?.Item).Where(x => x != null && x.Item_ == ItemId.ItemGreatBall).FirstOrDefault()?.Count ?? 0;
            var ultraBallsCount = inventory.InventoryDelta.InventoryItems.Select(x => x.InventoryItemData?.Item).Where(x => x != null && x.Item_ == ItemId.ItemUltraBall).FirstOrDefault()?.Count ?? 0;
            var masterBallsCount = inventory.InventoryDelta.InventoryItems.Select(x => x.InventoryItemData?.Item).Where(x => x != null && x.Item_ == ItemId.ItemMasterBall).FirstOrDefault()?.Count ?? 0;

            System.Console.WriteLine($"Pokeballs Left: {pokeBallsCount + greatBallsCount + ultraBallsCount}");

            if (masterBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_MASTER_BALL;
            else if (ultraBallsCount > 0 && pokemonCp >= 1500)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (ultraBallsCount > 0 && pokemonCp >= 1500)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (pokeBallsCount > 0)
                return MiscEnums.Item.ITEM_POKE_BALL;
            if (greatBallsCount > 0)
                return MiscEnums.Item.ITEM_GREAT_BALL;
            if (ultraBallsCount > 0)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            if (masterBallsCount > 0)
                return MiscEnums.Item.ITEM_MASTER_BALL;

            return MiscEnums.Item.ITEM_POKE_BALL;
        }

        private static string GetFriendlyItemsString(IEnumerable<FortSearchResponse.Types.ItemAward> items)
        {
            var enumerable = items as IList<FortSearchResponse.Types.ItemAward> ?? items.ToList();

            if (!enumerable.Any())
                return string.Empty;

            return
                enumerable.GroupBy(i => i.ItemId)
                          .Select(kvp => new { ItemName = kvp.Key.ToString(), Amount = kvp.Sum(x => x.ItemCount) })
                          .Select(y => $"{y.Amount} x {y.ItemName}")
                          .Aggregate((a, b) => $"{a}, {b}");
        }

        static async Task RecycleItems(Client client)
        {
            var inventory = await client.GetInventory();

            var itemRecycleList = new List<ItemId>
            {
                ItemId.ItemSuperPotion,
                ItemId.ItemPotion,
                ItemId.ItemRevive,
                ItemId.ItemMaxPotion,
                ItemId.ItemHyperPotion,
            };

            var items = inventory.InventoryDelta.InventoryItems
                .Select(i => i.InventoryItemData?.Item)
                .Where(p => p != null && itemRecycleList.Contains(p.Item_));

            foreach (var item in items.Where(x => x.Count > 0))
            {
                var transfer = await client.RecycleItem((AllEnum.ItemId)item.Item_, item.Count);
                System.Console.WriteLine($"Recycled {item.Count}x {(AllEnum.ItemId)item.Item_}");
                await Task.Delay(500);
            }
        }

        static List<FortData> SortRoute(List<FortData> route)
        {
            if (route.Count < 3)
            {
                return route;
            }
            route = route.OrderBy(x => Distance(x.Latitude, x.Longitude, Settings.DefaultLatitude, Settings.DefaultLongitude)).ToList();
            foreach (var stop in route)
            {
                System.Console.WriteLine("PokeStop Distance: " + Distance(stop.Latitude, stop.Longitude, Settings.DefaultLatitude, Settings.DefaultLongitude));
            }
            var newRoute = new List<FortData> { route.First() };
            route.RemoveAt(0);
            int i = 0;
            while (route.Any())
            {
                var next = route.OrderBy(x => Distance(x.Latitude, x.Longitude, newRoute.Last().Latitude, newRoute.Last().Longitude)).First();
                newRoute.Add(next);
                route.Remove(next);
                i++;
                if (i > 50)
                {
                    break;
                }
            }
            return newRoute;
        }

        //private static double Distance(double lat1, double long1, double lat2, double long2)
        //{
        //    return Math.Sqrt(Math.Pow(lat1 - lat2, 2) + Math.Pow(long1 - lat2, 2));
        //}

        private static double Distance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radius of the earth in km
            var dLat = deg2rad(lat2 - lat1);  // deg2rad below
            var dLon = deg2rad(lon2 - lon1);
            var a =
              Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
              Math.Cos(deg2rad(lat1)) * Math.Cos(deg2rad(lat2)) *
              Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
              ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in km
            return d;
        }

        private static double deg2rad(double deg)
        {
            return deg * (Math.PI / 180);
        }

    }
}
