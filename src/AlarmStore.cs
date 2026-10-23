using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AlarmClock
{
    [DataContract]
    internal sealed class AlarmRecord
    {
        public const int DefaultMinute = 6 * 60;

        [DataMember] public int InstanceId;
        [DataMember] public int PrefabIndex;
        [DataMember] public int Minute = DefaultMinute;
        [DataMember] public bool Enabled;
        [DataMember] public double NextDue;
    }

    [DataContract]
    internal sealed class AlarmSaveDocument
    {
        [DataMember(IsRequired = true)] public int Schema = 1;
        [DataMember(IsRequired = true)] public List<AlarmRecord> Alarms = new List<AlarmRecord>();
    }

    // Stored as one string in the native mod-data dictionary, so loading a save
    // never needs this assembly. Records for items that are not instantiated, such
    // as cached boat items, are kept because they are the only copy.
    internal sealed class AlarmStore
    {
        // The plugin GUID, repeated here so the logic tests can compile the store without BepInEx.
        public const string Key = "com.skeptic043.sailwind.alarmclock";
        private readonly Dictionary<int, AlarmRecord> records = new Dictionary<int, AlarmRecord>();

        public Dictionary<int, AlarmRecord>.ValueCollection Records => records.Values;
        public int Count => records.Count;
        // False after unreadable data, so saving leaves the original string untouched.
        public bool Writable { get; private set; } = true;
        public string Problem { get; private set; } = "";

        public void Reset()
        {
            records.Clear();
            Writable = true;
            Problem = "";
        }

        public bool Load(string json)
        {
            Reset();
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                AlarmSaveDocument document;
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    document = (AlarmSaveDocument)new DataContractJsonSerializer(typeof(AlarmSaveDocument)).ReadObject(stream);
                if (document == null || document.Schema != 1 || document.Alarms == null)
                    throw new SerializationException("unsupported alarm save schema");
                foreach (AlarmRecord record in document.Alarms)
                {
                    if (record == null || record.InstanceId <= 0 || record.Minute < 0 ||
                        record.Minute >= AlarmTime.MinutesPerDay || double.IsNaN(record.NextDue) ||
                        double.IsInfinity(record.NextDue))
                        throw new SerializationException("invalid alarm record");
                    if (records.ContainsKey(record.InstanceId)) throw new SerializationException("duplicate alarm identity");
                    records.Add(record.InstanceId, record);
                }
                return true;
            }
            catch (Exception ex) when (ex is SerializationException || ex is ArgumentException ||
                ex is System.Xml.XmlException || ex is FormatException || ex is InvalidCastException)
            {
                records.Clear();
                Writable = false;
                Problem = ex.Message;
                return false;
            }
        }

        public string Serialize()
        {
            var document = new AlarmSaveDocument();
            document.Alarms.AddRange(records.Values);
            document.Alarms.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(AlarmSaveDocument)).WriteObject(stream, document);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        // A different template under the same ID is not this item's record.
        public bool TryGet(int instanceId, int prefabIndex, out AlarmRecord record)
        {
            if (records.TryGetValue(instanceId, out record) && record.PrefabIndex == prefabIndex) return true;
            record = null;
            return false;
        }

        public AlarmRecord GetOrCreate(int instanceId, int prefabIndex)
        {
            if (TryGet(instanceId, prefabIndex, out var record)) return record;
            record = new AlarmRecord { InstanceId = instanceId, PrefabIndex = prefabIndex };
            records[instanceId] = record;
            return record;
        }
    }
}
