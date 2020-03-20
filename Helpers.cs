using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace KingmakerBuffBot
{
    class Helpers
    {
        public static void Log(string v)
        {
            UnityModManager.Logger.Log(v);
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
