﻿// Copyright (C) 2018 Tyler Szabo
//
// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with this program.  If not, see <http://www.gnu.org/licenses/>.

using GLedApiDotNet.LedSettings;
using System;
using System.Collections;
using System.Collections.Generic;

namespace GLedApiDotNet
{
    public class RGBFusionMotherboard : IRGBFusionMotherboard
    {
        private class MotherboardLedLayoutImpl : IReadOnlyList<LedType>
        {
            private readonly Lazy<LedType[]> myLayout;

            internal MotherboardLedLayoutImpl(Raw.GLedAPIv1_0_0Wrapper api, int maxDivisions)
            {
                this.Count = maxDivisions;
                myLayout = new Lazy<LedType[]>(() => {
                    byte[] rawLayout = api.GetLedLayout(maxDivisions);
                    if (maxDivisions != rawLayout.Length)
                    {
                        throw new GLedAPIException(string.Format("GetLedLayout({0}) returned {1} divisions", maxDivisions, rawLayout.Length));
                    }
                    LedType[] layout = new LedType[rawLayout.Length];
                    for (int i = 0; i < layout.Length; i++)
                    {
                        layout[i] = (LedType)rawLayout[i];
                    }
                    return layout;
                });
            }

            public LedType this[int i] => myLayout.Value[i];

            public int Count { get; }

            public IEnumerator<LedType> GetEnumerator()
            {
                foreach(LedType led in myLayout.Value)
                {
                    yield return led;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<LedType>)(myLayout.Value)).GetEnumerator();
        }

        private class MotherboardLedSettingsImpl : IList<LedSetting>
        {
            private LedSetting[] ledSettings;

            private bool dirty;

            internal void WriteToApi(Raw.GLedAPIv1_0_0Wrapper api)
            {
                if (dirty) {
                    // This is a workaround to avoid some zones/divisions not getting configured (issue #9).
                    // Calling SetLedData twice will actually allow all zones to be set.
                    api.SetLedData(this);
                    api.SetLedData(this); 
                    dirty = false;
                }
            }

            internal MotherboardLedSettingsImpl(MotherboardLedLayoutImpl layout, LedSetting defaultSetting)
            {
                dirty = true;
                ledSettings = new LedSetting[layout.Count];
                IEnumerator<LedType> e = layout.GetEnumerator();
                for (int i = 0; i < ledSettings.Length; i++)
                {
                    if(!e.MoveNext())
                    {
                        throw new GLedAPIException(string.Format("Number of layouts < length ({0})", ledSettings.Length));
                    }
                    ledSettings[i] = defaultSetting;
                }
            }

            public LedSetting this[int i]
            {
                get => ledSettings[i];
                set
                {
                    dirty = true;
                    ledSettings[i] = value;
                }
            }

            public int Count => ledSettings.Length;

            public bool IsReadOnly => false;

            public IEnumerator<LedSetting> GetEnumerator()
            {
                foreach(LedSetting ledSetting in ledSettings)
                {
                    yield return ledSetting;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<LedSetting>)ledSettings).GetEnumerator();

            public int IndexOf(LedSetting item) => throw new NotSupportedException();

            public void Insert(int index, LedSetting item) => throw new NotSupportedException();

            public void RemoveAt(int index) => throw new NotSupportedException();

            public void Add(LedSetting item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(LedSetting item) => throw new NotSupportedException();

            public void CopyTo(LedSetting[] array, int arrayIndex) => ledSettings.CopyTo(array, arrayIndex);

            public bool Remove(LedSetting item) => throw new NotSupportedException();
        }

        private Raw.GLedAPIv1_0_0Wrapper api;
        public int MaxDivisions { get; }

        private Lazy<MotherboardLedLayoutImpl> layout;
        public IReadOnlyList<LedType> Layout => layout.Value;

        private Lazy<MotherboardLedSettingsImpl> ledSettings;
        public IList<LedSetting> LedSettings => ledSettings.Value;

        internal RGBFusionMotherboard(Raw.GLedAPIv1_0_0Wrapper wrapperAPI)
        {
            api = wrapperAPI;

            string ver = api.GetSdkVersion();
            if (string.IsNullOrEmpty(ver))
            {
                throw new GLedAPIException(string.Format("GLedApi returned empty version"));
            }

            api.Initialize();

            MaxDivisions = api.GetMaxDivision();
            if (MaxDivisions == 0)
			{
                throw new GLedAPIException("No divisions");
            }

            layout = new Lazy<MotherboardLedLayoutImpl>(() => new MotherboardLedLayoutImpl(api, MaxDivisions));

            ledSettings = new Lazy<MotherboardLedSettingsImpl>(() => new MotherboardLedSettingsImpl(layout.Value, new OffLedSetting()));
        }

        public RGBFusionMotherboard() : this(new Raw.GLedAPIv1_0_0Wrapper())
        {
        }

        public void SetAll(LedSetting ledSetting)
        {
            for (int i = 0; i < ledSettings.Value.Count; i++)
            {
                ledSettings.Value[i] = ledSetting;
            }

            Set();
        }

        public void Set(params int[] divisions) { Set((IEnumerable<int>)divisions); }
        public void Set(IEnumerable<int> divisions)
        {
            int applyDivs = 0;
            foreach (int division in divisions)
            {
                if (division < 0 || division >= MaxDivisions)
                {
                    throw new ArgumentOutOfRangeException("divisions", division, "all divisions must be between 0 and MaxDivisions");
                }
                applyDivs |= (1 << division);
            }

            ledSettings.Value.WriteToApi(api);

            // Calling with no explicit divisions sets all
            api.Apply(applyDivs == 0 ? -1 : applyDivs);
        }
    }
}
