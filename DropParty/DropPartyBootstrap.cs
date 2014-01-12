using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Facepunch;
using LeatherLoader;
using UnityEngine;

namespace DropParty
{
    [Bootstrap]
    public class DropPartyBootstrap : Facepunch.MonoBehaviour
    {
		private string configDir;

        public void Awake()
        {
            //I copied this over from Dump Truck- don't think it's necessary for drop party, but don't have time to test removing it yet.
            //TOOD: Remember to see if I can take this out.
            DontDestroyOnLoad(this.gameObject);
        }

		public void ReceiveLeatherConfiguration(LeatherConfig config) {
			//Initialize config directory path where all the table files go
			configDir = Path.Combine(config.ConfigDirectoryPath, "DropParty");
		}

        public void Start()
        {
            //OnceLoaded gets called when all the bundles have finished loading into the game.  It's a good time to futz with the datablock dictionary
            //and other jerky things.
            Bundling.OnceLoaded += new Bundling.OnLoadedEventHandler(AssetsReady);
        }

        public void AssetsReady()
        {
            //Create config directory
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            //So I'm not sure how facepunch does it, but I need all the loot lists to be around when each one is populated so I can point them at
            //one another.  For that reason I pre-populate the loot table cache with empty ones.
            Dictionary<string, LootSpawnList> localSpawnListCache = new Dictionary<string,LootSpawnList>();
            foreach (string cfgFile in Directory.GetFiles(configDir, "*.cfg"))
            {
                localSpawnListCache[Path.GetFileNameWithoutExtension(cfgFile)] = ScriptableObject.CreateInstance<LootSpawnList>();
            }

            //Now let's repopulate these loot lists for real, from the contents of our config directory
            foreach (string cfgFile in Directory.GetFiles(configDir, "*.cfg"))
            {
                //Read in the files
                string tableName = Path.GetFileNameWithoutExtension(cfgFile);
                string[] lineArray = File.ReadAllLines(cfgFile);
                List<string> lines = new List<string>();

                //Remove blank lines & lines that are comments (we don't allow mid-line comments)
                foreach (string line in lineArray)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine[0] == '#')
                        continue;
                    lines.Add(trimmedLine);
                }

                LootSpawnList spawnList = null;
                
                //Try to parse this loot list from a file
                try 
                {
                    if (lines.Count() > 1)
                    {
                        spawnList = parseSpawnList(tableName, lines, localSpawnListCache);
                    } else
                    {
                        ConsoleSystem.LogError(string.Format("The loot table {0} did not have at least a header line and one entry.", tableName));
                    }                    
                } catch (Exception e)
                {
                    ConsoleSystem.LogException(e);
                }

                if (spawnList != null)
                {
                    //If we successfully parsed, overwrite the loot list in the spawn table with this one
                    DatablockDictionary._lootSpawnLists[tableName] = spawnList;
                    ConsoleSystem.Log(string.Format("Drop Party overwrote loot table {0}.", tableName));
                } else
                {
                    ConsoleSystem.LogError(string.Format("Drop Party could not parse loot table {0}!  This is really rather catastrophic!", tableName));
                }
            }
        }

        /// <summary>
        /// Parse a loot table out from the file contents
        /// </summary>
        /// <param name="tableName">The name of the loot table we're parsing</param>
        /// <param name="tableLines">The contents of the file we're parsing from, with non-data lines removed</param>
        /// <param name="cache">The cache of all the loot tables we're parsing today</param>
        /// <returns>A fully-parsed loot table, or null if we failed.</returns>
        private LootSpawnList parseSpawnList(string tableName, List<string> tableLines, Dictionary<string, LootSpawnList> cache)
        {
            //Get the first line (the header line), and split into 4 tokens
            LootSpawnList list = cache[tableName];
            string headerLine = tableLines[0];
            string[] headerTokens = headerLine.Split(new string[] {"\t"}, StringSplitOptions.RemoveEmptyEntries);

            if (headerTokens.Length != 4)
            {
                ConsoleSystem.LogError(string.Format("A loot table header line is supposed to have 4 entries, but {0}'s has {1}.", tableName, headerTokens.Length));
                return null;
            }

            //Parse the 4 tokens
            list.minPackagesToSpawn = int.Parse(headerTokens[0]);
            list.maxPackagesToSpawn = int.Parse(headerTokens[1]);
            list.noDuplicates = bool.Parse(headerTokens[2]);
            list.spawnOneOfEach = bool.Parse(headerTokens[3]);

            List<LootSpawnList.LootWeightedEntry> entries = new List<LootSpawnList.LootWeightedEntry>();
            //Remove the header line
            tableLines.RemoveAt(0);
            foreach (string line in tableLines)
            {
                //Split this entry line into 5 tokens
                string[] lineTokens = line.Split(new string[] { "\t" }, StringSplitOptions.RemoveEmptyEntries);

                if (lineTokens.Length != 5)
                {
                    ConsoleSystem.LogError(string.Format("Non-header lines in a loot table are supposed to have 5 entries, but {0} has a line with {1}: {2}", tableName, lineTokens.Length, lineTokens[2]));
                    return null;
                }

                LootSpawnList.LootWeightedEntry entry = new LootSpawnList.LootWeightedEntry();
                entry.weight = int.Parse(lineTokens[0]);

                //Figure out if we're of type T for Table or I for Item.  Parse token 3 into the correct table/item depending.
                if (lineTokens[1].Equals("T", StringComparison.OrdinalIgnoreCase))
                {
                    //If we have a loot table name, we try to pull from the cache of stuff we're parsing before falling back on the
                    //datablock dictionary, since the table we're looking for might be in another config file.
                    LootSpawnList reference = null;
                    if (cache.ContainsKey(lineTokens[2]))
                        reference = cache[lineTokens[2]];
                    else if (DatablockDictionary._lootSpawnLists.ContainsKey(lineTokens[2]))
                        reference = DatablockDictionary.GetLootSpawnListByName(lineTokens[2]);
                    
                    if (reference == null)
                    {
                        ConsoleSystem.LogError(string.Format("Loot table {0} has a reference to another loot table named {1}, but I can't find it anywhere!", tableName, lineTokens[2]));
                        return null;
                    }

                    entry.obj = reference;
                } else if (lineTokens[1].Equals("I", StringComparison.Ordinal))
                {
                    ItemDataBlock item = DatablockDictionary.GetByName(lineTokens[2]);

                    if (item == null)
                    {
                        ConsoleSystem.LogError(string.Format("Loot table {0} has a reference to an item named {1}, but it doesn't appear to exist!", lineTokens[2]));
                        return null;
                    }

                    entry.obj = item;
                } else
                {
                    ConsoleSystem.LogError(string.Format("Loot table {0} has an entry of type '{1}'.  The only valid types are T, for loot table, and I, for item."));
                    return null;
                }

                //Parse the rest of the entry
                entry.amountMin = int.Parse(lineTokens[3]);
                entry.amountMax = int.Parse(lineTokens[4]);
                entries.Add(entry);
            }

            //Finish parsing
            list.LootPackages = entries.ToArray();
            return list;
        }
    }
}
