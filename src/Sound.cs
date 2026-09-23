using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ScrollToArms
{
    /// <summary>
    /// A tick as the frame moves and a sound when a pick is lost, from the game's own sounds.
    ///
    /// Each cue is a copy of one of the game's sound prefabs, played the way the game plays its
    /// own: its ZSFX picks the clip, pitch and volume and asks AudioMan whether it may play, which
    /// applies the game's limit on the same sound repeating. Before the copy wakes up, its audio
    /// is sent to the game's interface mixer group, whose level the game sets from the master and
    /// sound effects volume, and made two-dimensional, so the cue sounds the same at any camera
    /// distance. The copy's network view is kept from starting, so only this player hears it, and
    /// it is destroyed on a timer in case the prefab does not tidy up after itself.
    /// </summary>
    internal static class Sound
    {
        public enum Cue
        {
            Tick,
            Lost
        }

        /// <summary>
        /// The game's sounds for each cue. A name that does not resolve leaves its cue silent
        /// rather than substituting another.
        /// </summary>
        private static readonly Dictionary<Cue, string> CueNames = new Dictionary<Cue, string>
        {
            { Cue.Tick, "sfx_gui_inventory_open" },
            { Cue.Lost, "sfx_gui_inventory_close" },
        };

        // Long enough for any interface sound to finish.
        private const float Lifetime = 5f;

        // Scrolling fast would otherwise stack ticks into a buzz.
        private const float MinTickGap = 0.04f;

        private static readonly Dictionary<Cue, GameObject> Cues = new Dictionary<Cue, GameObject>();
        private static bool _prepared;
        private static bool _muted;
        private static float _lastTick = -1f;
        private static GameObject _holder;

        /// <summary>
        /// Looks up every cue once, as soon as there is a world and a player. Most sounds are not
        /// registered with the scene, and finding them means indexing the game's effect lists,
        /// which takes a noticeable moment, so it is done on arrival rather than mid-fight.
        /// </summary>
        public static void Prepare()
        {
            if (_prepared || !Plugin.PlaySounds) return;
            if (ZNetScene.instance == null || Player.m_localPlayer == null) return;

            _prepared = true;
            foreach (var pair in CueNames)
            {
                var prefab = Find(pair.Value);
                Cues[pair.Key] = prefab;

                if (prefab == null) Plugin.Log.LogInfo($"ScrollToArms could not find \"{pair.Value}\"; that cue stays silent.");
            }
        }

        public static void Play(Cue cue)
        {
            if (_muted || !_prepared || !Plugin.PlaySounds) return;

            if (cue == Cue.Tick)
            {
                var now = Time.unscaledTime;
                if (now - _lastTick < MinTickGap) return;
                _lastTick = now;
            }

            if (Cues.TryGetValue(cue, out var prefab) && prefab != null) Play(prefab);
        }

        /// <summary>Silences every cue for good. Called on the way out.</summary>
        public static void Mute()
        {
            _muted = true;
        }

        private static void Play(GameObject prefab)
        {
            var was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                // Made under an inactive holder, so nothing on it wakes up, and so nothing plays,
                // until its audio is routed.
                var copy = UnityEngine.Object.Instantiate(prefab, Holder().transform, false);

                var gui = AudioMan.instance != null ? AudioMan.instance.m_guiMixer : null;
                foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
                {
                    if (gui != null) source.outputAudioMixerGroup = gui;
                    source.spatialBlend = 0f;
                }

                var camera = Utils.GetMainCamera();
                copy.transform.SetParent(null, false);
                copy.transform.position = camera != null ? camera.transform.position : Vector3.zero;

                UnityEngine.Object.Destroy(copy, Lifetime);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"ScrollToArms could not play a sound: {ex.Message}");
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        private static GameObject Holder()
        {
            if (_holder != null) return _holder;

            _holder = new GameObject("ScrollToArmsSounds");
            _holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_holder);
            return _holder;
        }

        /// <summary>
        /// A sound by name, wherever the game keeps it. <c>ZNetScene.GetPrefab</c> only knows the
        /// prefabs registered with the scene, and most sounds are not among them: they are
        /// referenced straight from the effect lists on the items, pieces, creatures, status
        /// effects and interface that play them.
        /// </summary>
        private static GameObject Find(string name)
        {
            var scene = ZNetScene.instance;
            if (scene == null || string.IsNullOrEmpty(name)) return null;

            var prefab = scene.GetPrefab(name);
            if (prefab != null) return prefab;

            var effects = Effects();
            return effects != null && effects.TryGetValue(name, out var found) ? found : null;
        }

        private static Dictionary<string, GameObject> _effects;
        private static readonly Dictionary<Type, FieldInfo[]> EffectFieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Every prefab an effect list in the game points at, by name. Built once, the first time
        /// a name misses the scene, by walking every registered prefab's components, the shared
        /// data and attacks of every item, every status effect and the interface. Sounds that
        /// belong only to locations stay out of reach, since walking locations would load them.
        /// </summary>
        private static Dictionary<string, GameObject> Effects()
        {
            if (_effects != null) return _effects;

            var scene = ZNetScene.instance;
            if (scene == null) return null;

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var index = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var name in scene.GetPrefabNames())
                {
                    var prefab = scene.GetPrefab(name);
                    if (prefab == null) continue;

                    foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                    {
                        if (component == null) continue;
                        Gather(component, index);

                        var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                        if (shared == null) continue;

                        Gather(shared, index);
                        if (shared.m_attack != null) Gather(shared.m_attack, index);
                        if (shared.m_secondaryAttack != null) Gather(shared.m_secondaryAttack, index);
                    }
                }

                var db = ObjectDB.instance;
                if (db != null)
                {
                    foreach (var effect in db.m_StatusEffects)
                    {
                        if (effect != null) Gather(effect, index);
                    }
                }

                // The interface is part of the scene rather than a prefab, so its sounds are only
                // reachable through the live objects, walked from each one's root.
                var roots = new HashSet<Transform>();
                AddRoot(InventoryGui.instance, roots);
                AddRoot(Hud.instance, roots);
                AddRoot(StoreGui.instance, roots);
                AddRoot(Minimap.instance, roots);
                AddRoot(MessageHud.instance, roots);
                AddRoot(Menu.instance, roots);
                AddRoot(Chat.instance, roots);

                foreach (var root in roots)
                {
                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null) Gather(component, index);
                    }
                }
            }
            catch (Exception ex)
            {
                // Whatever was gathered before the fault is still worth having.
                Plugin.Log.LogWarning($"ScrollToArms could not finish indexing the game's effects: {ex.Message}");
            }

            _effects = index;
            Plugin.Log.LogInfo($"ScrollToArms indexed {index.Count} effect prefabs in {watch.ElapsedMilliseconds} ms.");
            return _effects;
        }

        private static void AddRoot(Component part, HashSet<Transform> roots)
        {
            if (part != null) roots.Add(part.transform.root);
        }

        /// <summary>Adds whatever the effect lists on one object point at.</summary>
        private static void Gather(object owner, Dictionary<string, GameObject> index)
        {
            foreach (var field in EffectFields(owner.GetType()))
            {
                if (!(field.GetValue(owner) is EffectList list) || list.m_effectPrefabs == null) continue;

                foreach (var data in list.m_effectPrefabs)
                {
                    var prefab = data?.m_prefab;
                    if (prefab != null && !index.ContainsKey(prefab.name)) index[prefab.name] = prefab;
                }
            }
        }

        /// <summary>The EffectList fields on a type and its bases, remembered per type.</summary>
        private static FieldInfo[] EffectFields(Type type)
        {
            if (EffectFieldCache.TryGetValue(type, out var known)) return known;

            var found = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (field.FieldType == typeof(EffectList)) found.Add(field);
                }
            }

            known = found.ToArray();
            EffectFieldCache[type] = known;
            return known;
        }
    }
}
