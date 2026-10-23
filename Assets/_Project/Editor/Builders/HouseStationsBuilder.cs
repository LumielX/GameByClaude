using System.Collections.Generic;
using BooTrayal.Game.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static BooTrayal.Editor.Builders.BuilderUtil;

namespace BooTrayal.Editor.Builders
{
    /// <summary>
    /// The night's interactive pieces in the House (GDD 4, 7.4, 8): 3 safes with clue notes in other rooms, 3 fuse boxes
    /// with levers in two rooms each, the ritual altar, the shrine bell, blackout zones and meeting spots.
    /// Physical bodies go under Environment (so the NavMesh avoids them); networked roots sit at the scene root.
    /// </summary>
    public static class HouseStationsBuilder
    {
        const float Y = HouseBuilder.FloorY;
        static Font _font;
        static Material _metal, _key, _paper, _talisman, _red;

        /// <summary>Builds everything; returns the standing points every bot and the ghost must be able to reach.</summary>
        public static List<Vector3> Build(Scene scene, GameObject env)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _metal = Lit("SafeMetal", new Color(0.25f, 0.27f, 0.30f), 0.5f);
            _key = Lit("KeypadKey", new Color(0.82f, 0.78f, 0.66f));
            _paper = Lit("ClueNote", new Color(0.93f, 0.88f, 0.72f));
            _red = Lit("LeverHandle", new Color(0.8f, 0.15f, 0.12f), 0.4f);
            _talisman = GhostPrefabBuilder.Glowing(Lit("Talisman", new Color(1f, 0.82f, 0.2f)));
            var gold = Lit("BellGold", new Color(0.85f, 0.65f, 0.2f), 0.7f);
            var props = Child(env, "StationProps", Vector3.zero);
            var reach = new List<Vector3>();

            // Safes: body faces its keypad toward the room; the clue is in a different room.
            reach.Add(Safe(scene, props, 0, "A", new Vector3(-9.4f, Y, 3.8f), 90f, new Vector3(7f, Y + 1.5f, 4.88f), 180f));
            reach.Add(Safe(scene, props, 1, "B", new Vector3(-8f, 0f, -3.6f), 90f, new Vector3(-9.88f, Y + 1.5f, -2.5f), 90f));
            Primitive(props, "CluePost", PrimitiveType.Cube, new Vector3(8f, 0.8f, -12.2f), new Vector3(0.12f, 1.6f, 0.12f), _metal, true);
            reach.Add(Safe(scene, props, 2, "C", new Vector3(9.4f, Y, -3.8f), -90f, new Vector3(8f, 1.3f, -12.12f), 0f));

            // Fuse boxes: two levers in two places.
            Primitive(props, "LeverPostYard", PrimitiveType.Cube, new Vector3(15.15f, 0.8f, -5f), new Vector3(0.3f, 1.6f, 0.3f), _metal, true);
            Primitive(props, "LeverPostTerrace", PrimitiveType.Cube, new Vector3(2.4f, Y + 0.8f, 4.6f), new Vector3(0.3f, 1.6f, 0.3f), _metal, true);
            reach.AddRange(Fuse(scene, 3, "A", new Vector3(-9.88f, Y + 1.2f, -1.2f), 90f, new Vector3(-9f, 1.2f, -4.28f), 0f));
            reach.AddRange(Fuse(scene, 4, "B", new Vector3(5f, Y + 1.2f, 4.88f), 180f, new Vector3(14.98f, 1.2f, -5f), -90f));
            reach.AddRange(Fuse(scene, 5, "C", new Vector3(2.4f, Y + 1.2f, 4.43f), 180f, new Vector3(-5f, Y + 1.2f, 4.88f), 180f));

            reach.Add(Altar(scene, props));
            reach.Add(Bell(scene, gold));

            var zones = Root(scene, "Zones");
            Zone(zones, 0, "Under", new Vector3(0f, 1.2f, 0f), new Vector3(20f, 2.4f, 10f));
            Zone(zones, 1, "West", new Vector3(-6.5f, Y + 1.3f, 0f), new Vector3(7f, 2.6f, 10f));
            Zone(zones, 2, "Kitchen", new Vector3(6.5f, Y + 1.3f, 0f), new Vector3(7f, 2.6f, 10f));
            Zone(zones, 3, "Terrace", new Vector3(0f, Y + 1.3f, 0f), new Vector3(6f, 2.6f, 10f));
            Zone(zones, 4, "Yard", new Vector3(0f, 2f, -1f), new Vector3(50f, 4f, 42f));

            var spots = Root(scene, "MeetingSpots");
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var pos = new Vector3(3f * Mathf.Cos(a), 0f, -15f + 3f * Mathf.Sin(a));
                var spot = Child(spots, $"Spot{i}", pos);
                spot.transform.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Cos(a), 0f, -Mathf.Sin(a)));
                Ensure<MeetingSpot>(spot).Index = i;
                reach.Add(pos);
            }
            return reach;
        }

        static GameObject NetRoot(Scene scene, string name, Vector3 pos, float yaw)
        {
            var root = Root(scene, name);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Ensure<NetworkObject>(root);
            return root;
        }

        /// <summary>World-placed child (its parent may be far away, e.g. a clue note in another room).</summary>
        static GameObject Placed(GameObject parent, string name, Vector3 worldPos, float yaw)
        {
            var go = Child(parent, name, Vector3.zero);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>Text readable from the parent's front (+z).</summary>
        static TextMesh Label(GameObject parent, string name, Vector3 localPos, string text, float size, Color color)
        {
            var go = Child(parent, name, localPos);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var tm = Ensure<TextMesh>(go);
            tm.font = _font;
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            if (tm.text != text) tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            return tm;
        }

        static void Mark(GameObject parent, string name, Vector3 localPos) =>
            Primitive(parent, name, PrimitiveType.Quad, localPos, new Vector3(0.22f, 0.32f, 1f), _talisman, false, new Vector3(0f, 180f, 0f));

        static Interactable Part(GameObject go, NetworkBehaviour station, int part, string label, Transform approach)
        {
            var col = go.GetComponent<Collider>();
            col.isTrigger = true;
            var it = Ensure<Interactable>(go);
            it.Station = station;
            it.Part = part;
            it.Label = label;
            it.Approach = approach;
            return it;
        }

        static Vector3 Safe(Scene scene, GameObject props, int index, string letter, Vector3 pos, float yaw, Vector3 cluePos, float clueYaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Primitive(props, $"SafeBody{letter}", PrimitiveType.Cube, pos + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f), _metal, true, new Vector3(0f, yaw, 0f));
            var root = NetRoot(scene, $"Seal{index}_Safe{letter}", pos, yaw);
            var safe = Ensure<SafeStation>(root);
            safe.StationIndex = index;
            safe.Letter = letter;
            var approach = Child(root, "Approach", new Vector3(0f, 0f, 1.2f)).transform;

            var keypad = Child(root, "Keypad", new Vector3(0f, 0.72f, 0.42f));
            Label(keypad, "Letter", new Vector3(0f, 0.42f, 0.01f), letter, 0.05f, Color.white);
            for (int d = 0; d <= SafeStation.ClearPart; d++)
            {
                int row, col;
                if (d >= 1 && d <= 9) { row = (d - 1) / 3; col = (d - 1) % 3; }
                else if (d == 0) { row = 3; col = 1; }
                else { row = 3; col = 2; }
                var local = new Vector3((col - 1) * 0.16f, (1.5f - row) * 0.16f, 0f);
                var key = Primitive(keypad, $"Key{d}", PrimitiveType.Cube, local, new Vector3(0.13f, 0.13f, 0.06f), _key, true);
                string text = d == SafeStation.ClearPart ? "C" : d.ToString();
                Part(key, safe, d, text, approach);
                Label(keypad, $"KeyText{d}", local + new Vector3(0f, 0f, 0.035f), text, 0.03f, Color.black);
            }
            safe.Display = Label(keypad, "Display", new Vector3(0f, 0.32f, 0.01f), "____", 0.04f, new Color(0.3f, 1f, 0.55f));
            Mark(root, "SealMark", new Vector3(0f, 1.3f, 0.42f));

            var note = Placed(root, "ClueNote", cluePos, clueYaw);
            Primitive(note, "Board", PrimitiveType.Cube, Vector3.zero, new Vector3(0.5f, 0.36f, 0.02f), _paper, false);
            safe.Clue = Label(note, "Text", new Vector3(0f, 0f, 0.012f), $"SAFE {letter}\n- - - -", 0.035f, new Color(0.1f, 0.05f, 0.05f));
            return approach.position;
        }

        static IEnumerable<Vector3> Fuse(Scene scene, int index, string letter, Vector3 lever0, float yaw0, Vector3 lever1, float yaw1)
        {
            var root = NetRoot(scene, $"Seal{index}_Fuse{letter}", lever0, yaw0);
            var fuse = Ensure<FuseStation>(root);
            fuse.StationIndex = index;
            fuse.Handles = new Transform[2];
            var positions = new[] { lever0, lever1 };
            var yaws = new[] { yaw0, yaw1 };
            for (int i = 0; i < 2; i++)
            {
                var lever = Placed(root, $"Lever{i}", positions[i], yaws[i]);
                var approach = Child(lever, "Approach", new Vector3(0f, -1.2f, 0.9f)).transform;
                var box = Primitive(lever, "Box", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.45f, 0.12f), _metal, true);
                Part(box, fuse, i, $"Fuse {letter}{i + 1}", approach);
                var pivot = Child(lever, "Pivot", new Vector3(0f, 0f, 0.08f));
                Primitive(pivot, "Handle", PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.05f, 0.28f, 0.05f), _red, false);
                fuse.Handles[i] = pivot.transform;
                Label(lever, "Letter", new Vector3(0f, -0.32f, 0.07f), $"FUSE {letter}", 0.03f, Color.white);
                Mark(lever, $"SealMark{i}", new Vector3(0f, 0.45f, 0.07f));
                yield return approach.position;
            }
        }

        static Vector3 Altar(Scene scene, GameObject props)
        {
            var pos = new Vector3(-6f, 0f, -12f);
            Primitive(props, "AltarTable", PrimitiveType.Cube, pos + Vector3.up * 0.45f, new Vector3(1.6f, 0.9f, 1f), _paper, true);
            var root = NetRoot(scene, "RitualAltar", pos, 0f);
            var altar = Ensure<RitualAltar>(root);
            altar.RequiredIds = new[] { "urn", "incense" };
            altar.Slots = new[]
            {
                Child(root, "Slot0", new Vector3(-0.35f, 1.5f, 0f)).transform,
                Child(root, "Slot1", new Vector3(0.5f, 1.15f, 0f)).transform
            };
            Mark(root, "AltarMark", new Vector3(0f, 2.2f, -0.5f));
            return Child(root, "Approach", new Vector3(0f, 0f, -1.4f)).transform.position;
        }

        static Vector3 Bell(Scene scene, Material gold)
        {
            var root = NetRoot(scene, "ShrineBell", new Vector3(0f, 0f, -15f), 0f);
            var bell = Ensure<ShrineBell>(root);
            var approach = Child(root, "Approach", new Vector3(1.5f, 0f, 0f)).transform;
            var body = Primitive(root, "Bell", PrimitiveType.Cylinder, new Vector3(0.65f, 1.2f, 0f), new Vector3(0.22f, 0.12f, 0.22f), gold, true);
            ModelChild(body, "Bell");
            Part(body, bell, 0, "Bell", approach);
            return approach.position;
        }

        static void Zone(GameObject parent, int index, string name, Vector3 center, Vector3 size)
        {
            var z = Ensure<NightZone>(Child(parent, name, center));
            z.Index = index;
            z.ZoneName = name;
            z.Size = size;
        }
    }
}
