using BepInEx;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;
using Mirror;

namespace HuysHUD
{
    [BepInPlugin("com.huy.huyshud", "HuysHUD", "1.0.0")]
    public class MyPlugin : BaseUnityPlugin
    {
        // ── UI State ──────────────────────────────────────────────
        private bool showOverlay = false;
        private bool showOverlay2 = false;
        private int  activeTab   = 0; // 0=Game, 1=Props
        private int  activeTab2  = 2; // 2=Balance, 3=NPCs

        // Game tab
        private int    gameFieldIndex = 0;
        private string[] gameFieldLabels = new string[]
        {
            "GameManager.gold", "GameManager.totalScore", "PawnBlackboard._stamina",
            "PawnBlackboard._maxStamina", "PawnBlackboard._staminaRegenRate", "Pawn.CDR%"
        };
        private string[] gameFieldValues = new string[] { "0","0","0","0","0","0" };
        private float[]  gameFieldSteps  = new float[]  { 11, 1111, 10, 10, 0.5f, 5, 30f };

        // NPC tab
        private List<string> npcNames = new List<string>();
        private List<int>    npcMaxSpawn = new List<int>();
        private List<object> npcInstances = new List<object>();
        private int npcIndex = 0;
        private bool npcValuesRead = false;
        private float npcAppliedTime = -99f;
        private FieldInfo npcMaxSpawnField = null;

        // Balance tab
        private bool balanceEnabled = false;
        private int  balanceFieldIndex = 0;
        private string[] balanceLabels = new string[]
        {
            "Max NPC Tier", "Despawn Time", "Spawn Check Interval", "Min Dist to Players", "Max Active NPCs"
        };
        private float[] balanceValues = new float[] { 10f, 300f, 8f, 10f, 99f };
        private float[] balanceSteps  = new float[] {  1f,  10f, 1f, 1f,  1f };
        private float balanceApplyTimer = 0f;
        private bool balanceValuesRead = false;

        // Props tab
        private int propsCategory = 0;
        private string[] propsCategoryLabels = new string[] { "Potions", "Gold", "Wands", "Enemies" };

        private string[] potionKeys   = new string[] { "proppotion_maxhp","proppotion_cdr","proppotion_staminaboost","proppotion_doublejump" };
        private string[] potionLabels = new string[] { "Max HP","CDR","Stamina","Dbl Jump" };
        private int potionIndex = 0;

        private string[] goldKeys   = new string[] { "propgoldcoin","propgoldpile","propgoldbigpile","propgem" };
        private string[] goldLabels = new string[] { "Coin","Pile","Big Pile","Gem" };
        private int goldIndex = 0;

        private int wandIndex = 0;

        private string[] enemyKeys   = new string[] { "Frog", "Spider","Goat","Fetch","Eye","Slime","Goblin","Jester","BombMan","Guard","Gargoyle","UsEnemy","Merchant" };
        private string[] enemyLabels = new string[] { "Frog", "Spider","Goat","Fetch","Eye","Slime","Goblin","Jester","Bomber","Guard","Gargoyle","UsEnemy","Merchant" };
        private int enemyIndex = 0;

        // ── Styles ───────────────────────────────────────────────
        private GUIStyle styleTab, styleTabActive, styleLabel, styleLabelSelected;
        private GUIStyle styleField, styleHint, styleBtn, styleBtnSelected;
        private bool stylesInit = false;

        // ── Reflection ───────────────────────────────────────────
        private System.Type gmType, pbType, pawnType;
        private FieldInfo goldField, scoreField, staminaField, maxStaminaField, regenField, cdrField, roundTimeElapsedField;
        private PropertyInfo gmInstanceProp;
        private FieldInfo localInstanceField;

        // ── Spawn ────────────────────────────────────────────────
        private Dictionary<string, UnityEngine.Object> itemCache = new Dictionary<string, UnityEngine.Object>();
        private List<string> wandNames = new List<string>();
        private bool itemsCached = false;

        // ── Input debounce ───────────────────────────────────────
        private float lastNavTime = 0f;
        private float navDelay = 0.18f;

        void Awake()
        {
gmType = System.Type.GetType("YAPYAP.GameManager, Assembly-CSharp");
            if (gmType != null)
            {
                goldField             = gmType.GetField("gold",             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                scoreField            = gmType.GetField("totalScore",       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                gmInstanceProp        = gmType.GetProperty("Instance",      BindingFlags.Static   | BindingFlags.Public);
                roundTimeElapsedField = gmType.GetField("roundTimeElapsed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            pbType = System.Type.GetType("YAPYAP.PawnBlackboard, Assembly-CSharp");
            if (pbType != null)
            {
                staminaField    = pbType.GetField("_stamina",          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                maxStaminaField = pbType.GetField("_maxStamina",       BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                regenField      = pbType.GetField("_staminaRegenRate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            pawnType = System.Type.GetType("YAPYAP.Pawn, Assembly-CSharp");
            if (pawnType != null)
            {
                cdrField           = pawnType.GetField("_cdrMultiplier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                localInstanceField = pawnType.GetField("LocalInstance",  BindingFlags.Static   | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        // ── Getters/Setters ──────────────────────────────────────
        object GetGM()   { return gmInstanceProp    != null ? gmInstanceProp.GetValue(null, null) : null; }
        object GetPawn() { return localInstanceField != null ? localInstanceField.GetValue(null)   : null; }
        object GetPB()
        {
            if (pbType == null) return null;
            UnityEngine.Object[] all = UnityEngine.Object.FindObjectsOfType(pbType);
            PropertyInfo p = pbType.GetProperty("isLocalPlayer");
            foreach (var o in all) if (p != null && (bool)p.GetValue(o, null)) return o;
            return null;
        }

        float GetFieldValue(int idx)
        {
            switch (idx)
            {
                case 0: { object i=GetGM();   return (i!=null&&goldField       !=null)?(float)(int)goldField.GetValue(i)        :0; }
                case 1: { object i=GetGM();   return (i!=null&&scoreField      !=null)?(float)(int)scoreField.GetValue(i)       :0; }
                case 2: { object i=GetPB();   return (i!=null&&staminaField    !=null)?(float)staminaField.GetValue(i)          :0; }
                case 3: { object i=GetPB();   return (i!=null&&maxStaminaField !=null)?(float)maxStaminaField.GetValue(i)       :0; }
                case 4: { object i=GetPB();   return (i!=null&&regenField      !=null)?(float)regenField.GetValue(i)            :0; }
                case 5: { object i=GetPawn(); return (i!=null&&cdrField!=null)?Mathf.Round((1f-(float)cdrField.GetValue(i))*100f):0; }
                case 6: { object i=GetGM(); return (i!=null&&roundTimeElapsedField!=null)?(float)roundTimeElapsedField.GetValue(i):0; }
                default: return 0;
            }
        }

        void SetFieldValue(int idx, float val)
        {
            switch (idx)
            {
                case 0: { object i=GetGM();   if(i!=null&&goldField      !=null) goldField.SetValue(i,(int)Mathf.Max(0,val)); break; }
                case 1: { object i=GetGM();   if(i!=null&&scoreField     !=null) scoreField.SetValue(i,(int)Mathf.Max(0,val)); break; }
                case 2: { object i=GetPB();   if(i!=null&&staminaField   !=null) staminaField.SetValue(i,val); break; }
                case 3: { object i=GetPB();   if(i!=null&&maxStaminaField!=null) maxStaminaField.SetValue(i,val); break; }
                case 4: { object i=GetPB();   if(i!=null&&regenField     !=null) regenField.SetValue(i,val); break; }
                case 5: { object i=GetPawn(); if(i!=null&&cdrField!=null) cdrField.SetValue(i,Mathf.Clamp(1f-(val/100f),0.05f,1f)); break; }
                case 6: { object i=GetGM(); if(i!=null&&roundTimeElapsedField!=null) roundTimeElapsedField.SetValue(i,Mathf.Max(0f,val)); break; }
            }
        }

        void RefreshAllValues()
        {
            for (int i = 0; i < gameFieldLabels.Length; i++)
                gameFieldValues[i] = GetFieldValue(i).ToString("F1");
        }

        // ── Styles ───────────────────────────────────────────────
        Texture2D MakeTex(Color col) { Texture2D t=new Texture2D(1,1); t.SetPixel(0,0,col); t.Apply(); return t; }

        void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            styleLabel = new GUIStyle(GUI.skin.label);
            styleLabel.fontSize = 11;
            styleLabel.normal.textColor = new Color(0.85f,0.85f,0.85f);

            styleLabelSelected = new GUIStyle(styleLabel);
            styleLabelSelected.normal.textColor = new Color(1f,0.85f,0.3f);
            styleLabelSelected.fontStyle = FontStyle.Bold;

            styleField = new GUIStyle(GUI.skin.textField);
            styleField.fontSize = 11;

            styleHint = new GUIStyle(GUI.skin.label);
            styleHint.fontSize = 9;
            styleHint.normal.textColor = new Color(0.85f,0.85f,0.85f);

            styleTab = new GUIStyle(GUI.skin.button);
            styleTab.fontSize = 11;
            styleTab.normal.textColor = new Color(0.9f,0.9f,0.9f);

            styleTabActive = new GUIStyle(styleTab);
            styleTabActive.normal.textColor = Color.white;
            styleTabActive.fontStyle = FontStyle.Bold;
            styleTabActive.normal.background = MakeTex(new Color(0.25f,0.25f,0.45f,1f));

            styleBtn = new GUIStyle(GUI.skin.button);
            styleBtn.fontSize = 11;

            styleBtnSelected = new GUIStyle(styleBtn);
            styleBtnSelected.normal.textColor = new Color(1f,0.85f,0.3f);
            styleBtnSelected.fontStyle = FontStyle.Bold;
            styleBtnSelected.normal.background = MakeTex(new Color(0.3f,0.3f,0.15f,1f));
        }

        // ── Spawn ────────────────────────────────────────────────
        void CacheItems()
        {
            itemCache.Clear(); wandNames.Clear();
            Assembly asm = Assembly.Load("Assembly-CSharp");
            System.Type valType=null, puppetType=null, wandType=null;
            foreach (System.Type t in asm.GetTypes())
            {
                if (t.FullName=="YAPYAP.ValuableObject")        valType    = t;
                if (t.FullName=="YAPYAP.NetworkPuppetProp")     puppetType = t;
                if (t.FullName=="YAPYAP.NetworkPuppetWandProp") wandType   = t;
            }
            foreach (System.Type type in new System.Type[]{valType,puppetType})
            {
                if (type==null) continue;
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(type))
                {
                    if (obj==null) continue;
                    GameObject go = obj as GameObject;
                    if (go==null){Component c=obj as Component;if(c!=null)go=c.gameObject;}
                    if (go!=null&&go.scene.name==null)
                    {
                        string k=go.name.ToLower();
                        NetworkIdentity nid = go.GetComponent<NetworkIdentity>();
                        if (nid==null || nid.assetId==0) continue;
                        // Skip duplicates (names ending with " (N)")
                        if (k.Length > 3 && k[k.Length-1] == ')' && k[k.Length-3] == '(') continue;
                        // Prefer entries without bad placeholder assetId, but keep if nothing else
                        if (itemCache.ContainsKey(k)) continue;
                        itemCache[k]=obj;
                    }
                }
            }
            if (wandType!=null)
            {
                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(wandType))
                {
                    if (obj==null) continue;
                    GameObject go = obj as GameObject;
                    if (go==null){Component c=obj as Component;if(c!=null)go=c.gameObject;}
                    if (go!=null&&go.scene.name==null)
                    {
                        NetworkIdentity nid = go.GetComponent<NetworkIdentity>();
                        if (nid!=null && nid.assetId!=0)
                        {
                            string k=go.name.ToLower();
                            // Skip duplicates
                            if (k.Length > 3 && k[k.Length-1] == ')' && k[k.Length-3] == '(') continue;
                            wandNames.Add(go.name);
                            if (!itemCache.ContainsKey(k)) itemCache[k]=obj;
                        }
                    }
                }
            }
            // Deduplicate wand names
            List<string> seen = new List<string>();
            List<string> deduped = new List<string>();
            foreach (string w in wandNames)
            {
                if (!seen.Contains(w)) { seen.Add(w); deduped.Add(w); }
            }
            wandNames = deduped;
            // Filter wand names to only cached entries
            List<string> validWands = new List<string>();
            foreach (string w in wandNames)
                if (itemCache.ContainsKey(w.ToLower())) validWands.Add(w);
            wandNames = validWands;

            itemsCached = true;
            Logger.LogInfo(string.Format("[HuysHUD] Cached {0} items, {1} wands", itemCache.Count, wandNames.Count));
        }

        // Known correct assetIds from server (discovered via LogSpawnedObjects)
        uint GetCorrectAssetId(string searchKey, uint fallback)
        {
            string key = searchKey.ToLower();
            foreach (var kvp in NetworkClient.spawned)
            {
                if (kvp.Value == null) continue;
                string n = kvp.Value.gameObject.name.ToLower().Replace("(clone)", "").Trim();
                if (n.Contains(key) && kvp.Value.assetId != 0 && kvp.Value.assetId != 2433275061u)
                    return kvp.Value.assetId;
            }
            return fallback;
        }

        void LogSpawnedObjects()
        {
            Logger.LogInfo("=== NetworkClient.spawned ===");
            foreach (var kvp in NetworkClient.spawned)
            {
                if (kvp.Value == null) continue;
                string n = kvp.Value.gameObject.name.ToLower();
                if (n.Contains("gold") || n.Contains("potion") || n.Contains("wand") || n.Contains("gem"))
                    Logger.LogInfo(string.Format("  netId={0} assetId={1} name={2}", kvp.Key, kvp.Value.assetId, kvp.Value.gameObject.name));
            }
            Logger.LogInfo("=== End ===");
        }

        void SpawnEnemy(string enemyType)
        {
            try
            {
                if (!NetworkServer.active) { Logger.LogWarning("[HuysHUD] Enemy spawn: host only"); return; }
                object pawn = GetPawn();
                if (pawn==null) return;
                PropertyInfo tp = pawn.GetType().GetProperty("transform", BindingFlags.Instance|BindingFlags.Public);
                Transform tr = tp!=null?(Transform)tp.GetValue(pawn,null):null;
                if (tr==null) return;
                Vector3 pos = tr.position + tr.forward*3f;
                pos.y = tr.position.y;

                System.Type spawnerType = null;
                foreach (System.Type t in Assembly.Load("Assembly-CSharp").GetTypes())
                    if (t.Name=="DungeonNpcSpawner") { spawnerType=t; break; }
                if (spawnerType==null) return;

                PropertyInfo instProp = spawnerType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                if (instProp==null) return;
                object spawnerInst = instProp.GetValue(null, null);
                if (spawnerInst==null) { Logger.LogWarning("[HuysHUD] DungeonNpcSpawner.Instance is null - not in dungeon"); return; }

                FieldInfo npcsField = spawnerType.GetField("npcs", BindingFlags.Instance|BindingFlags.NonPublic);
                if (npcsField==null) return;
                Array npcs = npcsField.GetValue(spawnerInst) as Array;
                if (npcs==null||npcs.Length==0) return;

                string key = enemyType.ToLower();
                object found = null;
                foreach (object npc in npcs)
                {
                    Component c = npc as Component;
                    if (c!=null && c.gameObject.name.ToLower().Contains(key)) { found=npc; break; }
                }
                if (found==null) { Logger.LogWarning("[HuysHUD] Enemy not found: "+enemyType); return; }

                Component comp = found as Component;
                NetworkIdentity nid = comp.gameObject.GetComponent<NetworkIdentity>();
                if (nid==null) return;

                System.Type gmType2 = System.Type.GetType("YAPYAP.GameManager, Assembly-CSharp");
                MethodInfo m = null;
                foreach (MethodInfo mi in gmType2.GetMethods(BindingFlags.Static|BindingFlags.Public))
                {
                    ParameterInfo[] prms = mi.GetParameters();
                    if (mi.Name=="SpawnNetworkPrefab" && prms.Length==4 && prms[0].ParameterType==typeof(NetworkIdentity))
                    { m=mi; break; }
                }
                if (m!=null) m.Invoke(null, new object[]{nid, pos, Quaternion.identity, true});
            }
            catch(Exception ex){Logger.LogError("[HuysHUD] SpawnEnemy: "+ex.Message);}
        }

        void SpawnItem(string searchKey)
        {
            try
            {
                if (!itemsCached) CacheItems();
                object pawn = GetPawn();
                if (pawn==null) { Logger.LogWarning("[HuysHUD] Local pawn is null"); return; }
                PropertyInfo tp = pawn.GetType().GetProperty("transform", BindingFlags.Instance|BindingFlags.Public);
                Transform tr = tp!=null?(Transform)tp.GetValue(pawn,null):null;
                if (tr==null) { Logger.LogWarning("[HuysHUD] Transform is null"); return; }
                Vector3 pos = tr.position + tr.forward*2f + Vector3.up*0.5f;

                string key = searchKey.ToLower();
                UnityEngine.Object found = null;
                foreach (var kvp in itemCache) if(kvp.Key.Contains(key)){found=kvp.Value;break;}
                if (found==null) { Logger.LogWarning("[HuysHUD] Not in cache: "+searchKey); return; }

                GameObject go = found as GameObject;
                if (go==null){Component c=found as Component;if(c!=null)go=c.gameObject;}
                if (go==null) { Logger.LogWarning("[HuysHUD] No GameObject for: "+searchKey); return; }

                NetworkIdentity nid = go.GetComponent<NetworkIdentity>();
                if (nid==null) { Logger.LogWarning("[HuysHUD] No NetworkIdentity on: "+go.name); return; }
                if (nid.assetId==0) { Logger.LogWarning("[HuysHUD] assetId is 0 for: "+go.name); return; }

                object gm = GetGM();
                if (gm==null) { Logger.LogWarning("[HuysHUD] GameManager is null"); return; }

                Logger.LogInfo(string.Format("[HuysHUD] Spawning {0} | assetId={1} | host={2}", go.name, nid.assetId, NetworkServer.active));

                if (NetworkServer.active)
                {
                    System.Type gmType2 = System.Type.GetType("YAPYAP.GameManager, Assembly-CSharp");
                    MethodInfo m = null;
                    foreach (MethodInfo mi in gmType2.GetMethods(BindingFlags.Static|BindingFlags.Public))
                    {
                        ParameterInfo[] prms = mi.GetParameters();
                        if (mi.Name=="SpawnNetworkPrefab" && prms.Length==4 && prms[0].ParameterType==typeof(NetworkIdentity))
                        { m=mi; break; }
                    }
                    if (m!=null) m.Invoke(null, new object[]{nid, pos, Quaternion.identity, true});
                    else Logger.LogWarning("[HuysHUD] SpawnNetworkPrefab not found");
                }
                else
                {
                    uint spawnAssetId = GetCorrectAssetId(go.name.ToLower().Replace("prop",""), nid.assetId);
                    if (spawnAssetId==2433275061u) { Logger.LogWarning("[HuysHUD] No valid assetId for client: "+go.name); return; }
                    Logger.LogInfo(string.Format("[HuysHUD] Client using assetId={0} for {1}", spawnAssetId, go.name));
                    MethodInfo mc = gm.GetType().GetMethod("CmdRequestSpawnNetworkPrefab",
                        BindingFlags.Instance|BindingFlags.Public, null,
                        new System.Type[]{typeof(uint),typeof(Vector3),typeof(Quaternion)}, null);
                    if (mc!=null) mc.Invoke(gm, new object[]{spawnAssetId, pos, Quaternion.identity});
                    else Logger.LogWarning("[HuysHUD] CmdRequestSpawnNetworkPrefab not found");
                }
            }
            catch(Exception ex){Logger.LogError("[HuysHUD] Spawn error: "+ex.Message);}
        }

        // ── Update ───────────────────────────────────────────────
        void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.KeypadMultiply))
            {
                showOverlay = !showOverlay;
                if (showOverlay) { showOverlay2=false; RefreshAllValues(); if(!itemsCached)CacheItems(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
                else { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            }
            if (Input.GetKeyDown(KeyCode.KeypadPeriod))
            {
                showOverlay2 = !showOverlay2;
                if (showOverlay2) { showOverlay=false; Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
                else { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            }
            bool anyOpen = showOverlay || showOverlay2;
            if (!anyOpen) return;
            // Apply balance continuously when enabled
            if (balanceEnabled)
            {
                balanceApplyTimer -= Time.unscaledDeltaTime;
                if (balanceApplyTimer <= 0f) { ApplyBalance(); balanceApplyTimer = 1f; }
            }

            if (!showOverlay && !showOverlay2) return;

            bool canNav = Time.unscaledTime - lastNavTime > navDelay;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (showOverlay) activeTab = activeTab==0 ? 1 : 0;
                if (showOverlay2) activeTab2 = activeTab2==2 ? 3 : 2;
                // Don't auto-read - user must press Num0 or Re-read
                lastNavTime = Time.unscaledTime;
                return;
            }

            if (showOverlay)
            {
                if (activeTab==0) HandleGameTab(canNav);
                else              HandlePropsTab(canNav);
            }
            if (showOverlay2)
            {
                if (activeTab2==2) HandleBalanceTab(canNav);
                else               HandleNpcTab(canNav);
            }

            if (Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (showOverlay  && activeTab==1)  SpawnCurrentProp();
                if (showOverlay2 && activeTab2==2) { balanceEnabled = !balanceEnabled; if (balanceEnabled) ApplyBalance(); }
                if (showOverlay2 && activeTab2==3) ApplyNpcValues();
            }
            if (Input.GetKeyDown(KeyCode.Keypad0))
            {
                if (showOverlay2 && activeTab2==2) ReadBalanceValues();
                if (showOverlay2 && activeTab2==3) ReadNpcValues();
            }
            if (Input.GetKeyDown(KeyCode.KeypadDivide) && showOverlay2 && activeTab2==3) ApplyNpcPreset();
        }

        void HandleGameTab(bool canNav)
        {
            if (canNav && (Input.GetKey(KeyCode.Keypad8)||Input.GetKey(KeyCode.Keypad2)))
            {
                int dir = Input.GetKey(KeyCode.Keypad2) ? 1 : -1;
                gameFieldIndex = (gameFieldIndex+dir+gameFieldLabels.Length)%gameFieldLabels.Length;
                lastNavTime = Time.unscaledTime;
            }
            if (canNav && (Input.GetKey(KeyCode.KeypadPlus)||Input.GetKey(KeyCode.KeypadMinus)))
            {
                float dir = Input.GetKey(KeyCode.KeypadPlus) ? 1f : -1f;
                float cur = GetFieldValue(gameFieldIndex);
                SetFieldValue(gameFieldIndex, cur + dir*gameFieldSteps[gameFieldIndex]);
                gameFieldValues[gameFieldIndex] = GetFieldValue(gameFieldIndex).ToString("F1");
                lastNavTime = Time.unscaledTime;
            }
        }

        void HandlePropsTab(bool canNav)
        {
            if (canNav && (Input.GetKey(KeyCode.Keypad4)||Input.GetKey(KeyCode.Keypad6)))
            {
                int dir = Input.GetKey(KeyCode.Keypad6) ? 1 : -1;
                propsCategory = (propsCategory+dir+4)%4;
                lastNavTime = Time.unscaledTime;
            }
            if (canNav && (Input.GetKey(KeyCode.Keypad8)||Input.GetKey(KeyCode.Keypad2)))
            {
                bool fwd = Input.GetKey(KeyCode.Keypad2);
                int dir = fwd ? 1 : -1;
                if (propsCategory==0) potionIndex=(potionIndex+dir+potionKeys.Length)%potionKeys.Length;
                else if (propsCategory==1) goldIndex=(goldIndex+dir+goldKeys.Length)%goldKeys.Length;
                else if (propsCategory==2 && wandNames.Count>0) wandIndex=(wandIndex+dir+wandNames.Count)%wandNames.Count;
                else if (propsCategory==3) enemyIndex=(enemyIndex+dir+enemyKeys.Length)%enemyKeys.Length;
                lastNavTime = Time.unscaledTime;
            }
        }

        void SpawnCurrentProp()
        {
            if (propsCategory==0) SpawnItem(potionKeys[potionIndex]);
            else if (propsCategory==1) SpawnItem(goldKeys[goldIndex]);
            else if (propsCategory==2 && wandNames.Count>0) SpawnItem(wandNames[wandIndex]);
            else if (propsCategory==3) SpawnEnemy(enemyKeys[enemyIndex]);
        }

        // ── Balance ──────────────────────────────────────────────
        void HandleBalanceTab(bool canNav)
        {
            if (canNav && (Input.GetKey(KeyCode.Keypad8)||Input.GetKey(KeyCode.Keypad2)))
            {
                int dir = Input.GetKey(KeyCode.Keypad2) ? 1 : -1;
                balanceFieldIndex = (balanceFieldIndex+dir+balanceLabels.Length)%balanceLabels.Length;
                lastNavTime = Time.unscaledTime;
            }
            if (canNav && (Input.GetKey(KeyCode.KeypadPlus)||Input.GetKey(KeyCode.KeypadMinus)))
            {
                float dir = Input.GetKey(KeyCode.KeypadPlus) ? 1f : -1f;
                balanceValues[balanceFieldIndex] = Mathf.Max(0f, balanceValues[balanceFieldIndex] + dir*balanceSteps[balanceFieldIndex]);
                lastNavTime = Time.unscaledTime;
            }

        }

        void LogAllSpawnerValues()
        {
            System.Type spawnerType = System.Type.GetType("YAPYAP.DungeonNpcSpawner, Assembly-CSharp");
            if (spawnerType==null) return;
            PropertyInfo instP = spawnerType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
            object inst = instP!=null?instP.GetValue(null,null):null;
            if (inst==null) { Logger.LogInfo("[HuysHUD] DungeonNpcSpawner.Instance is null"); return; }
            Logger.LogInfo("=== DungeonNpcSpawner live values ===");
            foreach (FieldInfo f in spawnerType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                try { Logger.LogInfo(string.Format("  {0} {1} = {2}", f.FieldType.Name, f.Name, f.GetValue(inst))); }
                catch {}
            }
            // Also check SelectSpawnPoint method for distance logic
            Logger.LogInfo("=== End ===");
        }

        void LogSpawnerFields()
        {
            System.Type spawnerType = System.Type.GetType("YAPYAP.DungeonNpcSpawner, Assembly-CSharp");
            if (spawnerType == null) { Logger.LogWarning("[HuysHUD] DungeonNpcSpawner not found"); return; }
            Logger.LogInfo("=== DungeonNpcSpawner fields ===");
            foreach (FieldInfo f in spawnerType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                string name = f.Name.ToLower();
                if (name.Contains("dist") || name.Contains("spawn") || name.Contains("despawn") || name.Contains("time") || name.Contains("min"))
                    Logger.LogInfo("  " + f.FieldType.Name + " " + f.Name);
            }
        }

        void ReadBalanceValues()
        {
            try
            {
                System.Type dmType = System.Type.GetType("YAPYAP.DungeonManager, Assembly-CSharp");
                if (dmType != null)
                {
                    PropertyInfo instP = dmType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                    object dmInst = instP != null ? instP.GetValue(null, null) : null;
                    if (dmInst != null)
                    {
                        PropertyInfo lcProp = dmType.GetProperty("CurrentLevelConfig", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        object lc = lcProp != null ? lcProp.GetValue(dmInst, null) : null;
                        if (lc != null)
                        {
                            FieldInfo maxNpcs = lc.GetType().GetField("maxActiveNpcs", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            FieldInfo maxTier = lc.GetType().GetField("maxNpcTier",    BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            if (maxNpcs != null) balanceValues[4] = (float)(int)maxNpcs.GetValue(lc);
                            if (maxTier != null) balanceValues[0] = (float)(int)maxTier.GetValue(lc);
                        }
                    }
                }

                System.Type spawnerType = System.Type.GetType("YAPYAP.DungeonNpcSpawner, Assembly-CSharp");
                if (spawnerType != null)
                {
                    PropertyInfo instP2 = spawnerType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                    object spawnInst = instP2 != null ? instP2.GetValue(null, null) : null;
                    if (spawnInst != null)
                    {
                        FieldInfo despawnField = spawnerType.GetField("despawnTime",              BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        FieldInfo minDistField = spawnerType.GetField("minimumDistanceToPlayers", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        if (minDistField==null) minDistField = spawnerType.GetField("minDistanceToPlayers", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        if (minDistField==null) minDistField = spawnerType.GetField("minimumDistance", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

                        FieldInfo spawnIntervalField = spawnerType.GetField("spawnCheckInterval", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        if (despawnField      != null) balanceValues[1] = (float)despawnField.GetValue(spawnInst);
                        if (spawnIntervalField!= null) balanceValues[2] = (float)spawnIntervalField.GetValue(spawnInst);
                        if (minDistField      != null) balanceValues[3] = (float)minDistField.GetValue(spawnInst);
                    }
                }
                balanceValuesRead = true;
                Logger.LogInfo(string.Format("[HuysHUD] Balance read: maxTier={0} despawn={1} interval={2} minDist={3} maxNpcs={4}",
                    balanceValues[0], balanceValues[1], balanceValues[2], balanceValues[3], balanceValues[4]));
            }
            catch(Exception ex) { Logger.LogError("[HuysHUD] ReadBalanceValues: "+ex.Message); }
        }

        void ApplyBalance()
        {
            try
            {
                // Apply LevelConfig fields
                System.Type dmType = System.Type.GetType("YAPYAP.DungeonManager, Assembly-CSharp");
                if (dmType != null)
                {
                    PropertyInfo instP = dmType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                    object dmInst = instP != null ? instP.GetValue(null, null) : null;
                    if (dmInst != null)
                    {
                        PropertyInfo lcProp = dmType.GetProperty("CurrentLevelConfig", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        object lc = lcProp != null ? lcProp.GetValue(dmInst, null) : null;
                        if (lc != null)
                        {
                            FieldInfo maxNpcs = lc.GetType().GetField("maxActiveNpcs", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            FieldInfo maxTier = lc.GetType().GetField("maxNpcTier",    BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            if (maxNpcs != null) maxNpcs.SetValue(lc, (int)balanceValues[4]);
                            if (maxTier != null) maxTier.SetValue(lc, (int)balanceValues[0]);
                        }
                    }
                }

                // Apply DungeonNpcSpawner fields
                System.Type spawnerType = System.Type.GetType("YAPYAP.DungeonNpcSpawner, Assembly-CSharp");
                if (spawnerType != null)
                {
                    PropertyInfo instP2 = spawnerType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                    object spawnInst = instP2 != null ? instP2.GetValue(null, null) : null;
                    if (spawnInst != null)
                    {
                        // Power level cap - set spawnedPowerLevel field
                        FieldInfo plField      = spawnerType.GetField("spawnedPowerLevel",        BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        FieldInfo despawnField = spawnerType.GetField("despawnTime",               BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        FieldInfo minDistField = spawnerType.GetField("minimumDistanceToPlayers",  BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        FieldInfo nextSpawnField = spawnerType.GetField("nextSpawnTimes",          BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        FieldInfo maxActiveField  = spawnerType.GetField("maxActiveNpcs",          BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

                        int autoPowerCap = (int)balanceValues[4] * 4 + 10;
                        FieldInfo spawnIntervalField2 = spawnerType.GetField("spawnCheckInterval", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        if (despawnField            != null) despawnField.SetValue(spawnInst,       balanceValues[1]);
                        if (minDistField       != null)
                        {
                            minDistField.SetValue(spawnInst, balanceValues[3]);
                            Logger.LogInfo(string.Format("[HuysHUD] minDist after={0}", minDistField.GetValue(spawnInst)));
                        }
                        if (spawnIntervalField2!= null) spawnIntervalField2.SetValue(spawnInst,balanceValues[2]);
                        FieldInfo minDistReadField = spawnerType.GetField("minimumDistanceToPlayers", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                        if (minDistReadField!=null)
                            minDistReadField.SetValue(spawnInst, balanceValues[3]);
                        // Reset spawnedPowerLevel so NPCs can spawn again
                        if (plField != null) plField.SetValue(spawnInst, 0);

                        // Reset spawnedPowerLevel to 0 every apply cycle to bypass power level cap
                        if (plField != null) plField.SetValue(spawnInst, 0);
                        if (nextSpawnField != null)
                        {
                            System.Collections.IDictionary dict = nextSpawnField.GetValue(spawnInst) as System.Collections.IDictionary;
                            if (dict != null)
                            {
                                System.Collections.ArrayList keys = new System.Collections.ArrayList();
                                foreach (object k in dict.Keys) keys.Add(k);
                                foreach (object k in keys) dict[k] = 0f;
                            }
                        }
                    }
                }
            }
            catch(Exception ex) { Logger.LogError("[HuysHUD] ApplyBalance: "+ex.Message); }
        }

        void DrawBalanceTab()
        {
            string toggleLabel = balanceEnabled ? "● ENABLED" : "○ DISABLED";
            GUIStyle toggleStyle = new GUIStyle(styleBtn);
            toggleStyle.normal.textColor = balanceEnabled ? new Color(0.3f,1f,0.3f) : new Color(1f,0.4f,0.4f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Balance Override:", styleLabel, GUILayout.Width(130));
            if (GUILayout.Button(toggleLabel, toggleStyle))
            {
                balanceEnabled = !balanceEnabled;
                if (balanceEnabled) ApplyBalance();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Num8/2 cycle  |  Num+/- change  |  NumEnter toggle  |  Num0 re-read", styleHint);
            if (GUILayout.Button("Re-read", styleBtn, GUILayout.Width(55))) { ReadBalanceValues(); }
            if (GUILayout.Button("Log Fields", styleBtn, GUILayout.Width(70))) LogSpawnerFields();
            if (GUILayout.Button("Log All", styleBtn, GUILayout.Width(60))) LogAllSpawnerValues();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            for (int i=0; i<balanceLabels.Length; i++)
            {
                bool sel = i==balanceFieldIndex;
                GUILayout.BeginHorizontal();
                GUILayout.Label((sel?"▶ ":"   ")+balanceLabels[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(200));
                GUILayout.Label(balanceValues[i].ToString("F0"), sel?styleLabelSelected:styleLabel, GUILayout.Width(60));
                GUILayout.Label("±"+balanceSteps[i].ToString("G"), styleHint, GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(string.Format("  → Power Level Cap (auto): {0}", (int)balanceValues[0]*4+10), styleHint);
        }

        // ── NPC Tab ──────────────────────────────────────────────
        void ReadNpcValues()
        {
            npcNames.Clear();
            npcMaxSpawn.Clear();
            npcInstances.Clear();
            try
            {
                System.Type spawnerType = System.Type.GetType("YAPYAP.DungeonNpcSpawner, Assembly-CSharp");
                if (spawnerType==null) return;
                PropertyInfo instP = spawnerType.GetProperty("Instance", BindingFlags.Static|BindingFlags.Public);
                object spawnInst = instP!=null?instP.GetValue(null,null):null;
                if (spawnInst==null) return;
                FieldInfo npcsField = spawnerType.GetField("npcs", BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                if (npcsField==null) return;
                Array npcs = npcsField.GetValue(spawnInst) as Array;
                if (npcs==null) return;

                System.Type npcType = System.Type.GetType("YAPYAP.NpcBehaviour, Assembly-CSharp");
                if (npcType!=null)
                    npcMaxSpawnField = npcType.GetField("maxSpawnedAmount", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

                foreach (object npc in npcs)
                {
                    if (npc==null) continue;
                    Component c = npc as Component;
                    if (c==null) continue;
                    string name = c.gameObject.name;
                    // Skip duplicates
                    if (npcNames.Contains(name)) continue;
                    int maxSpawn = 0;
                    if (npcMaxSpawnField!=null)
                        maxSpawn = (int)npcMaxSpawnField.GetValue(npc);
                    npcNames.Add(name);
                    npcMaxSpawn.Add(maxSpawn);
                    npcInstances.Add(npc);
                }
                npcValuesRead = true;
                Logger.LogInfo(string.Format("[HuysHUD] NPC tab read {0} types", npcNames.Count));
            }
            catch(Exception ex){ Logger.LogError("[HuysHUD] ReadNpcValues: "+ex.Message); }
        }

        void ApplyNpcPreset()
        {
            // Preset: balanced caps
            System.Collections.Generic.Dictionary<string,int> preset = new System.Collections.Generic.Dictionary<string,int>
            {
                {"NpcEnemyJester", 3},
                {"NpcBombMan", 5},
                {"NpcEyeMonster", 5},
                {"NpcGoblin", 5},
                {"NpcSlimeMonster", 5},
                {"NpcEnemyGoatman", 5},
                {"NpcFetchPawn", 5},
                {"NpcEnemySpider", 5},
            };
            for (int i=0; i<npcNames.Count; i++)
            {
                foreach (var kvp in preset)
                    if (npcNames[i].Contains(kvp.Key)) { npcMaxSpawn[i] = kvp.Value; break; }
            }
        }

        void ApplyNpcValues()
        {
            npcAppliedTime = Time.unscaledTime;
            try
            {
                for (int i=0; i<npcInstances.Count; i++)
                {
                    if (npcMaxSpawnField!=null && npcInstances[i]!=null)
                        npcMaxSpawnField.SetValue(npcInstances[i], npcMaxSpawn[i]);
                }
            }
            catch(Exception ex){ Logger.LogError("[HuysHUD] ApplyNpcValues: "+ex.Message); }
        }

        void HandleNpcTab(bool canNav)
        {
            if (npcNames.Count==0) return;
            if (canNav && (Input.GetKey(KeyCode.Keypad8)||Input.GetKey(KeyCode.Keypad2)))
            {
                int dir = Input.GetKey(KeyCode.Keypad2)?1:-1;
                npcIndex = (npcIndex+dir+npcNames.Count)%npcNames.Count;
                lastNavTime = Time.unscaledTime;
            }
            if (canNav && (Input.GetKey(KeyCode.KeypadPlus)||Input.GetKey(KeyCode.KeypadMinus)))
            {
                float dir = Input.GetKey(KeyCode.KeypadPlus)?1f:-1f;
                npcMaxSpawn[npcIndex] = Mathf.Max(0, npcMaxSpawn[npcIndex]+(int)dir);
                lastNavTime = Time.unscaledTime;
            }

        }

        void DrawNpcTab()
        {
            if (!npcValuesRead || npcNames.Count==0)
            {
                GUILayout.Label("Not in dungeon or no NPCs found.", styleLabel);
                GUILayout.BeginHorizontal();
            if (GUILayout.Button("Num0: Read NPCs", styleBtn)) ReadNpcValues();
            if (GUILayout.Button("NumEnter: Apply", styleBtn)) ApplyNpcValues();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Num/: Apply Preset (J3 B5 E5 G5 S5 Gt5 F5 Sp5)", styleBtn)) ApplyNpcPreset();
                return;
            }
            GUILayout.Label("Num8/2 cycle  |  Num+/- change  |  Num0 read  |  NumEnter apply", styleHint);
            if (Time.unscaledTime - npcAppliedTime < 2f)
            {
                GUIStyle styleFeedback = new GUIStyle(styleLabel);
                styleFeedback.normal.textColor = new Color(0.3f, 1f, 0.3f);
                GUILayout.Label("✓ Applied!", styleFeedback);
            }
            GUILayout.Space(2);
            int start = Mathf.Max(0, npcIndex-3);
            int end   = Mathf.Min(npcNames.Count-1, start+6);
            for (int i=start; i<=end; i++)
            {
                bool sel = i==npcIndex;
                GUILayout.BeginHorizontal();
                GUILayout.Label((sel?"▶ ":"   ")+npcNames[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(230));
                GUILayout.Label(npcMaxSpawn[i].ToString(), sel?styleLabelSelected:styleLabel, GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Num0: Read NPCs", styleBtn)) ReadNpcValues();
            if (GUILayout.Button("NumEnter: Apply", styleBtn)) ApplyNpcValues();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Num/: Apply Preset (J3 B5 E5 G5 S5 Gt5 F5 Sp5)", styleBtn)) ApplyNpcPreset();
        }

        // ── GUI ──────────────────────────────────────────────────
        void DrawWindow2(int id)
        {
            InitStyles();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Balance", activeTab2==2?styleTabActive:styleTab, GUILayout.Height(22))) activeTab2=2;
            if (GUILayout.Button("NPCs",    activeTab2==3?styleTabActive:styleTab, GUILayout.Height(22))) { activeTab2=3; if(!npcValuesRead) ReadNpcValues(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            if (activeTab2==2) DrawBalanceTab();
            else               DrawNpcTab();
            GUILayout.Space(4);
            GUILayout.Label("Num. toggle  |  Tab switch  |  Num* for Game/Props", styleHint);
        }

        
        void OnGUI()
        {
            if (!showOverlay && !showOverlay2) return;
            InitStyles();
            Color prev=GUI.color;
            GUI.color=new Color(1f,1f,1f,0.95f);
            float w=340f, cy=Screen.height-440f;
            if (showOverlay && showOverlay2)
            {
                GUI.Window(9999, new Rect((Screen.width/2f)-w-10f, cy, w, 300f), DrawWindow,  "Huy's HUD");
                GUI.Window(9998, new Rect((Screen.width/2f)+10f,   cy, w, 300f), DrawWindow2, "Huy's HUD - Balance");
            }
            else if (showOverlay)
                GUI.Window(9999, new Rect((Screen.width-w)/2f, cy, w, 300f), DrawWindow,  "Huy's HUD");
            else
                GUI.Window(9998, new Rect((Screen.width-w)/2f, cy, w, 300f), DrawWindow2, "Huy's HUD - Balance");
            GUI.color=prev;
        }

        void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Game",  activeTab==0?styleTabActive:styleTab, GUILayout.Height(22))) activeTab=0;
            if (GUILayout.Button("Props", activeTab==1?styleTabActive:styleTab, GUILayout.Height(22))) activeTab=1;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            if (activeTab==0) DrawGameTab();
            else              DrawPropsTab();

            GUILayout.Space(4);
            if (activeTab==1 && GUILayout.Button("Log Spawned", styleBtn)) LogSpawnedObjects();
            GUILayout.Label("Num* toggle  |  Tab switch  |  Num. for Balance/NPCs", styleHint);
            GUI.DragWindow();
        }

        void DrawGameTab()
        {
            GUILayout.Label("Num8/2 cycle  |  Num+/- change value  (Round Time: - adds time)", styleHint);
            GUILayout.Space(2);
            for (int i=0; i<gameFieldLabels.Length; i++)
            {
                bool sel = i==gameFieldIndex;
                GUILayout.BeginHorizontal();
                GUILayout.Label((sel?"▶ ":"   ")+gameFieldLabels[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(200));
                GUILayout.Label(gameFieldValues[i], sel?styleLabelSelected:styleLabel, GUILayout.Width(80));
                GUILayout.Label("±"+gameFieldSteps[i].ToString("G"), styleHint, GUILayout.Width(40));
                GUILayout.EndHorizontal();
            }
        }

        void DrawPropsTab()
        {
            GUILayout.BeginHorizontal();
            for (int i=0; i<propsCategoryLabels.Length; i++)
            {
                bool sel = i==propsCategory;
                if (GUILayout.Button(propsCategoryLabels[i], sel?styleTabActive:styleTab, GUILayout.Height(20)))
                    propsCategory=i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Num4/6 category  |  Num8/2 cycle item  |  NumEnter spawn", styleHint);
            GUILayout.Space(4);

            if (propsCategory==0)
            {
                for (int i=0; i<potionLabels.Length; i++)
                    GUILayout.Label((i==potionIndex?"▶ ":"   ")+potionLabels[i], i==potionIndex?styleLabelSelected:styleLabel);
                GUILayout.Space(4);
                if (GUILayout.Button("Spawn: "+potionLabels[potionIndex], styleBtn)) SpawnItem(potionKeys[potionIndex]);
            }
            else if (propsCategory==1)
            {
                for (int i=0; i<goldLabels.Length; i++)
                    GUILayout.Label((i==goldIndex?"▶ ":"   ")+goldLabels[i], i==goldIndex?styleLabelSelected:styleLabel);
                GUILayout.Space(4);
                if (GUILayout.Button("Spawn: "+goldLabels[goldIndex], styleBtn)) SpawnItem(goldKeys[goldIndex]);
            }
            else if (propsCategory==2)
            {
                if (wandNames.Count==0) { GUILayout.Label("No wands cached", styleLabel); return; }
                GUILayout.Label("Num8/2 cycle wands:", styleLabel);
                int start=Mathf.Max(0,wandIndex-2), end=Mathf.Min(wandNames.Count-1,start+4);
                for (int i=start; i<=end; i++)
                {
                    bool sel = i==wandIndex;
                    GUILayout.Label((sel?"▶ ":"   ")+wandNames[i], sel?styleLabelSelected:styleLabel);
                }
                GUILayout.Space(4);
                if (GUILayout.Button("Spawn: "+wandNames[wandIndex], styleBtn)) SpawnItem(wandNames[wandIndex]);
            }
            else if (propsCategory==3)
            {
                GUILayout.Label("Num8/2 cycle  |  Host only", styleHint);
                int estart=Mathf.Max(0,enemyIndex-2), eend=Mathf.Min(enemyLabels.Length-1,estart+4);
                for (int i=estart; i<=eend; i++)
                {
                    bool sel = i==enemyIndex;
                    GUILayout.Label((sel?"▶ ":"   ")+enemyLabels[i], sel?styleLabelSelected:styleLabel);
                }
                GUILayout.Space(4);
                if (GUILayout.Button("Spawn: "+enemyLabels[enemyIndex], styleBtn)) SpawnEnemy(enemyKeys[enemyIndex]);
            }
        }
    }
}