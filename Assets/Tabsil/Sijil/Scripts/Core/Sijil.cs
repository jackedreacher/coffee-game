using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using Leguar.TotalJSON;
using System;
using System.Reflection;
using UnityEngine.SceneManagement;


namespace Tabsil.Sijil
{
    public class Sijil : MonoBehaviour
    {
        public static Sijil instance;

        private string dataPath;

        public static GameData GameData { get; private set; }

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else
                Destroy(gameObject);

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;

#if UNITY_EDITOR
            dataPath = Application.dataPath + "/GameData.txt";
#else
        dataPath = Application.persistentDataPath + "/GameData.txt";
#endif

            LoadData();

            DontDestroyOnLoad(this);
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            LoadSaveables();
        }

        private void Start()
        {
            //Load();
        }

        private void LocalSave()
        {
            StreamWriter writer = new StreamWriter(dataPath);

            JSON gameDataJSon = JSON.Serialize(GameData);
            string dataString = gameDataJSon.CreatePrettyString();

            writer.WriteLine(dataString);

            writer.Close();
        }

        private void LoadData()
        {
            if (!File.Exists(dataPath))
            {
                GameData = new GameData();
                LocalSave();
            }
            else
            {
                StreamReader reader = new StreamReader(dataPath);
                string dataString = reader.ReadToEnd();
                reader.Close();

                JSON gameDataJson = JSON.ParseString(dataString);
                GameData = gameDataJson.Deserialize<GameData>();
            }
        }

        private void LoadSaveables()
        {
            foreach (IWantToBeSaved saveable in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IWantToBeSaved>())
                saveable.Load();
        }

        private static void Save()
        {
            instance.LocalSave();
        }

        public static void Save(object sender, string key, object data)
        {
            string fullKey = GetFullKey(sender, key);
            string jsonData;
            Type dataType = data.GetType();

            Type serializableOjectType = typeof(SerializableObject<>).MakeGenericType(dataType);
            object mySerializableObject = Activator.CreateInstance(serializableOjectType);

            FieldInfo myObjectField = serializableOjectType.GetField("myObject");

            // Set the myList field of the serializableList
            myObjectField.SetValue(mySerializableObject, data);

            // Finally Serialize it to JSon and save
            jsonData = JSON.Serialize(mySerializableObject).CreatePrettyString();

            // Before saving, set the dataType to SerializeList<>
            dataType = serializableOjectType;

            GameData.Add(fullKey, dataType, jsonData);
            Save();
        }

        public static bool TryLoad(object sender, string key, out object value)
        {
            string fullKey = GetFullKey(sender, key);

            if (GameData.TryGetValue(fullKey, out Type dataType, out string data))
            {
                DeserializeSettings settings = new DeserializeSettings();

                JSON jsonObject = JSON.ParseString(data);

                value = jsonObject.zDeserialize(dataType, "fieldName", settings);

                FieldInfo myObject = value.GetType().GetField("myObject");

                value = myObject.GetValue(value);

                return true;
            }

            value = null;
            return false;
        }

        public static void Remove(object sender, string key)
        {
            string fullKey = GetFullKey(sender, key);
            GameData.Remove(fullKey);
            Save();
        }

        private static string GetFullKey(object sender, string key)
        {
            string scriptType = sender == null ? "" : sender.GetType().ToString();
            string fullKey = scriptType + "_" + key;

            return fullKey;
        }


    }

    [Serializable]
    struct SerializableObject<T>
    {
        [SerializeField] public T myObject;
    }
}