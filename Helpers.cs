using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace WrathBuffBot
{
    class Helpers
    {
        public static UnityModManager.ModEntry modInfo = null;
        public static void Log(string v)
        {
#if DEBUG
            modInfo.Logger.Log(v + " - " + DateTime.Now.ToString());
#endif
        }
        public static void Label(string label)
        {
            GUILayout.Label(label);
        }
        public static void BH()
        {
            GUILayout.BeginHorizontal();
        }
        public static void EH()
        {
            GUILayout.EndHorizontal();
        }
        public static void BV()
        {
            GUILayout.BeginHorizontal();
        }
        public static void EV()
        {
            GUILayout.EndHorizontal();
        }
    }
}
