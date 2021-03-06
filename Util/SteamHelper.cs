﻿﻿using System;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace HKExporter.Util {
    public static class SteamHelper {
        private static readonly int HOLLOWKNIGHT_APP_ID = 367520;
        private static readonly string HOLLOWKNIGHT_GAME_NAME = "Hollow Knight";

        public static string GetHollowKnightDataPath() {
            var gamePath = SteamHelper.GetHollowKnightPath();

            if (gamePath == "" || !Directory.Exists(gamePath)) {
                Debug.LogError("Could not find Hollow Knight path.");
                return null;
            }

            var gameDataPath = Path.Combine(gamePath, "hollow_knight_Data");

            return gameDataPath;
        }

        public static string GetHollowKnightPath() {
            return FindSteamGamePath(HOLLOWKNIGHT_APP_ID, HOLLOWKNIGHT_GAME_NAME);
        }

        private static string FindSteamGamePath(int appid, string gameName) {
            if (ReadRegistrySafe("Software\\Valve\\Steam", "SteamPath") == null) {
                Debug.LogError("You either don't have steam installed or your registry variable isn't set.");
                return "";
            }

            string appsPath = Path.Combine((string) ReadRegistrySafe("Software\\Valve\\Steam", "SteamPath"),
                "steamapps");

            if (File.Exists(Path.Combine(appsPath, "appmanifest_" + appid + ".acf"))) {
                return Path.Combine(Path.Combine(appsPath, "common"), gameName);
            }

            string path = SearchAllInstallations(Path.Combine(appsPath, "libraryfolders.vdf"), appid, gameName);
            if (path == null) {
                Debug.LogError("It appears you don't have " + gameName + " installed anywhere. The game files are needed to find the game.");
            }
            else {
                return path;
            }

            return "";
        }

        private static string SearchAllInstallations(string libraryfolders, int appid, string gameName) {
            StreamReader file = new StreamReader(libraryfolders);
            string line;
            while ((line = file.ReadLine()) != null) {
                line.Trim();
                line.Trim('\t');
                line = Regex.Unescape(line);
                Match regMatch = Regex.Match(line, "\"(.*)\"\t*\"(.*)\"");
                string key = regMatch.Groups[1].Value;
                string value = regMatch.Groups[2].Value;
                int number;
                if (int.TryParse(key, out number)) {
                    if (File.Exists(Path.Combine(value, "steamapps/appmanifest_" + appid + ".acf"))) {
                        return Path.Combine(Path.Combine(value, "steamapps/common"), gameName);
                    }
                }
            }

            return null;
        }

        private static object ReadRegistrySafe(string path, string key) {
            using (RegistryKey subkey = Registry.CurrentUser.OpenSubKey(path)) {
                if (subkey != null) {
                    return subkey.GetValue(key);
                }
            }

            return null;
        }
    }
}