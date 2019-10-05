﻿// Copyright (C) 2018 Tyler Szabo
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with this program.  If not, see <http://www.gnu.org/licenses/>.

using GLedApiDotNet;
using GLedApiDotNet.LedSettings;
using GvLedLibDotNet;
using GvLedLibDotNet.GvLedSettings;
using System;
using System.IO;
using Mono.Options;
using System.Collections.Generic;
using RGBFusionTool.ArgParsers;
using RGBFusionTool.ArgParsers.LedSettings;
using RGBFusionTool.ArgParsers.GvLedSettings;

namespace RGBFusionTool
{
    public class Application
    {
        private Func<IRGBFusionMotherboard> motherboardFactory;
        private Func<IRGBFusionPeripherals> peripheralsFactory;
        private TextWriter stdout;
        private TextWriter stderr;

        private class ApplicationContext
        {
            public int Verbosity { get; set; }
            public bool ShowHelp { get; set; }
            public bool ShowVersion { get; set; }
            public bool ListZones { get; set; }
            public bool ListPeripherals { get; set; }
            public void ListAll()
            {
                ListZones = true;
                ListPeripherals = true;
            }

            public ApplicationContext()
            {
                SetDefaults();
            }

            public void SetDefaults()
            {
                Verbosity = 0;
                ShowHelp = false;
                ShowVersion = false;
                ListZones = false;
                ListPeripherals = false;
            }
        }
        ApplicationContext context;

        private Dictionary<int, List<string>> zones = new Dictionary<int, List<string>>();
        private List<string> currentZone;
        private List<string> defaultZone;
        private List<string> peripheralsArgs;

        OptionSet genericOptions;
        OptionSet zoneOptions;
        List<LedSettingArgParser<LedSetting>> ledSettingArgParsers;
        List<LedSettingArgParser<GvLedSetting>> gvLedSettingArgParsers;

        List<OptionSet> helpOptionSets;

        public Application(Func<IRGBFusionMotherboard> motherboardFactory, Func<IRGBFusionPeripherals> peripheralsFactory, TextWriter stdout, TextWriter stderr)
        {
            this.motherboardFactory = motherboardFactory;
            this.peripheralsFactory = peripheralsFactory;
            this.stdout = stdout;
            this.stderr = stderr;

            context = new ApplicationContext();

            defaultZone = new List<string>();
            currentZone = defaultZone;
            peripheralsArgs = new List<string>();

            genericOptions = new OptionSet
            {
                { string.Format("Usage: {0} [OPTION]... [[LEDSETTING] | [ZONE LEDSETTING]...] [peripherals GVSETTING]", AppDomain.CurrentDomain.FriendlyName) },
                { "Set RGB Fusion motherboard LEDs" },
                { "" },
                { "Options:" },
                { "v|verbose", v => context.Verbosity++ },
                { "l|list", "list zones", v => context.ListZones = true },
                { "list-peripherals", "list peripherals", v => context.ListPeripherals = true },
                { "la|list-all", "list peripherals", v => context.ListAll() },
                { "?|h|help", "show help and exit", v => context.ShowHelp = true  },
                { "version", "show version information and exit", v => context.ShowVersion = true },
                { "" }
            };

            zoneOptions = new OptionSet
            {
                { "ZONE:" },
                { "z|zone=", "set zone", (int zone) => {
                    if (zone < 0)
                    {
                        throw new InvalidOperationException("Zones must be positive integers");
                    }
                    if (zones.ContainsKey(zone))
                    {
                        throw new InvalidOperationException(string.Format("Zone {0} already specified", zone));
                    }
                    currentZone = new List<string>();
                    zones.Add(zone, currentZone);
                } },
                { "PERIPHERALS:" },
                { "peripherals", "set peripherals", v => {
                    currentZone = new List<string>();
                    peripheralsArgs = currentZone;
                } },
                { "<>", v => currentZone.Add(v) },
            };

            ledSettingArgParsers = new List<LedSettingArgParser<LedSetting>>
            {
                new StaticColorArgParser(),
                new ColorCycleArgParser(),
                new PulseArgParser(),
                new FlashArgParser(),
                new DigitalAArgParser(),
                new DigitalBArgParser(),
                new DigitalCArgParser(),
                new DigitalDArgParser(),
                new DigitalEArgParser(),
                new DigitalFArgParser(),
                new DigitalGArgParser(),
                new DigitalHArgParser(),
                new DigitalIArgParser(),
                new OffArgParser(),
            };

            gvLedSettingArgParsers = new List<LedSettingArgParser<GvLedSetting>>
            {
                new OffGvArgParser(),
                new StaticColorGvArgParser(),
                new ColorCycleGvArgParser()
            };

            helpOptionSets = new List<OptionSet>
            {
                genericOptions,
                zoneOptions,
                new OptionSet { "" },
                new OptionSet { "LEDSETTING options:" }
            };
            foreach (LedSettingArgParser argParser in ledSettingArgParsers)
            {
                helpOptionSets.Add(new OptionSet { "" });
                helpOptionSets.Add(argParser.RequiredOptions);
                helpOptionSets.Add(argParser.ExtraOptions);
            }
            helpOptionSets.Add(new OptionSet { "" });
            helpOptionSets.Add(new OptionSet { "GVSETTING options:" });
            foreach (LedSettingArgParser argParser in gvLedSettingArgParsers)
            {
                helpOptionSets.Add(new OptionSet { "" });
                helpOptionSets.Add(argParser.RequiredOptions);
                helpOptionSets.Add(argParser.ExtraOptions);
            }
        }

        private void ShowHelp(TextWriter o)
        {
            foreach (OptionSet option in helpOptionSets)
            {
                option.WriteOptionDescriptions(o);
            }
        }

        private void ShowVersion(TextWriter o)
        {
            string gplNotice =
@"This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.";

            o.WriteLine("RGB Fusion Tool {0}", System.Reflection.Assembly.GetAssembly(this.GetType()).GetName().Version);
            o.WriteLine("Copyright (C) 2018-2019  Tyler Szabo");
            o.WriteLine();
            o.WriteLine(gplNotice);
            o.WriteLine();
            o.WriteLine("Source: https://github.com/tylerszabo/RGB-Fusion-Tool");
        }

        public void Main(string[] args)
        {
            context.SetDefaults();

            try
            {
                List<string> afterGeneric = genericOptions.Parse(args);

                if (context.ShowHelp)
                {
                    ShowHelp(stdout);
                    return;
                }

                if (context.ShowVersion)
                {
                    ShowVersion(stdout);
                    return;
                }

                zoneOptions.Parse(afterGeneric);

                Lazy<IRGBFusionMotherboard> motherboardLEDs = new Lazy<IRGBFusionMotherboard>(motherboardFactory);
                Lazy<IRGBFusionPeripherals> peripheralLEDs = new Lazy<IRGBFusionPeripherals>(peripheralsFactory);

                if (context.ListPeripherals || (context.Verbosity > 0 && peripheralsArgs.Count > 0))
                {
                    for (int i = 0; i < peripheralLEDs.Value.Devices.Length; i++)
                    {
                        stdout.WriteLine("Peripheral {0}: {1}", i, peripheralLEDs.Value.Devices[i]);
                    }
                }
                if (context.ListZones || (context.Verbosity > 0 && (defaultZone.Count > 0 || zones.Count > 0)))
                {
                    for (int i = 0; i < motherboardLEDs.Value.Layout.Length; i++)
                    {
                        stdout.WriteLine("Zone {0}: {1}", i, motherboardLEDs.Value.Layout[i]);
                    }
                }

                if (defaultZone.Count == 0 && zones.Count == 0 && peripheralsArgs.Count == 0)
                {
                    return;
                }
                else if (defaultZone.Count > 0 && zones.Count > 0)
                {
                    throw new InvalidOperationException(string.Format("Unexpected options {0} before zone-specific options", string.Join(" ", defaultZone.ToArray())));
                }

                if (defaultZone.Count > 0)
                {
                    LedSetting setting = null;
                    foreach (LedSettingArgParser<LedSetting> parser in ledSettingArgParsers)
                    {
                        setting = parser.TryParse(defaultZone);
                        if (setting != null) { break; }
                    }

                    if (setting == null) { throw new InvalidOperationException("No LED mode specified"); }

                    if (context.Verbosity > 0)
                    {
                        stdout.WriteLine("Set All: {0}", setting);
                    }
                    motherboardLEDs.Value.SetAll(setting);
                    return;
                }
                else if (zones.Count > 0)
                {
                    foreach (int zone in zones.Keys)
                    {
                        if (zone >= motherboardLEDs.Value.Layout.Length)
                        {
                            throw new InvalidOperationException(string.Format("Zone is {0}, max supported is {1}", zone, motherboardLEDs.Value.Layout.Length));
                        }

                        LedSetting setting = null;
                        foreach (LedSettingArgParser<LedSetting> parser in ledSettingArgParsers)
                        {
                            setting = parser.TryParse(zones[zone]);
                            if (setting != null) { break; }
                        }

                        motherboardLEDs.Value.LedSettings[zone] = setting ?? throw new InvalidOperationException(string.Format("No LED mode specified for zone {0}", zone));
                        if (context.Verbosity > 0)
                        {
                            stdout.WriteLine("Set zone {0}: {1}", zone, motherboardLEDs.Value.LedSettings[zone]);
                        }
                    }

                    motherboardLEDs.Value.Set(zones.Keys);
                }

                if (peripheralsArgs.Count > 0)
                {
                    GvLedSetting setting = null;
                    foreach (LedSettingArgParser<GvLedSetting> parser in gvLedSettingArgParsers)
                    {
                        setting = parser.TryParse(peripheralsArgs);
                        if (setting != null) { break; }
                    }

                    if (setting == null) { throw new InvalidOperationException("No Peripheral LED mode specified"); }

                    if (context.Verbosity > 0)
                    {
                        stdout.WriteLine("Set All Peripherals: {0}", setting);
                    }
                    peripheralLEDs.Value.SetAll(setting);
                    return;
                }
            }
            catch (Exception e)
            {
                ShowHelp(stderr);
                stderr.WriteLine();
                stderr.WriteLine("Error: {0}", e.ToString());
                throw;
            }
            return;
        }
    }
}
