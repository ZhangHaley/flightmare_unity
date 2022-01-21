
using System.Collections.Generic;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

using MessageSpec;


namespace RPGFlightmare
{
    // Idea from: https://www.tutorialfor.com/questions-7293.htm
    public class terrainTreeManager1 : MonoBehaviour
    {
        // ==============================================================================
        // Default Parameters 
        // ==============================================================================
        private const Int16 ORIGIN = 0;
        private const Int16 PLACE = 1;
        private const Int16 REMOVE = 2;
        private const Int16 PLACE_PARAM = 3;

        // ==============================================================================
        // Public Parameters


        //================================================================================
        // NetWork
        //===============================================================================
        private Dictionary<string, Action> eventDictionary;
        private Dictionary<string, Action<EventParam>> eventDictionary_param;

        //===============================================================================
        // Private Parameters
        //===============================================================================

        private static terrainTreeManager1 eventManager;

        private static Int16 Option = ORIGIN;
        public static Action placeFunction = new Action(placeTerrain);
        public static Action<EventParam> placeFunctionParam = new Action<EventParam>(placeTerrainParam);
        public static Action removeFunction = new Action(removeTerrain);

        private static readonly Type terrainDataType = typeof(TerrainData);
        private static Terrain terrain;
        private static MassTreePlacement mtp = new MassTreePlacement();
        private static EventParam event_param = new EventParam();


        public static terrainTreeManager1 instance
        {
            get
            {
                if (!eventManager)
                {
                    eventManager = FindObjectOfType(typeof(terrainTreeManager1)) as terrainTreeManager1;

                    if (!eventManager)
                    {
                        Debug.LogError("There needs to be one active EventManger script on a GameObject in your scene.");
                    }
                    else
                    {
                        eventManager.Init();
                    }
                }

                return eventManager;
            }
        }
        

        void Init()
        {
            if (eventDictionary == null)
            {
                eventDictionary = new Dictionary<string, Action>();
            }
            if(eventDictionary_param==null)
            {
                eventDictionary_param= new Dictionary<string, Action<EventParam>>();
            }
            if (terrain == null)
            {
                terrain = Terrain.activeTerrain;
            }
        }

        public static void StartListening(string eventName, Action listener)
        {
            Action thisEvent;
            if (instance.eventDictionary.TryGetValue(eventName, out thisEvent))
            {
                //Add more event to the existing one
                thisEvent += listener;

                //Update the Dictionary
                instance.eventDictionary[eventName] = thisEvent;
            }
            else
            {
                //Add event to the Dictionary for the first time
                thisEvent += listener;
                instance.eventDictionary.Add(eventName, thisEvent);
            }
        }

        public static void StopListening(string eventName, Action listener)
        {
            if (eventManager == null) return;
            Action thisEvent;
            if (instance.eventDictionary.TryGetValue(eventName, out thisEvent))
            {
                //Remove event from the existing one
                thisEvent -= listener;

                //Update the Dictionary
                instance.eventDictionary[eventName] = thisEvent;
            }
        }

        public static void TriggerEvent(string eventName)
        {
            Action thisEvent = null;
            if (instance.eventDictionary.TryGetValue(eventName, out thisEvent))
            {
                thisEvent.Invoke();
                // OR USE instance.eventDictionary[eventName]();
            }
        }

        public static void StartListening(string eventName, Action<EventParam> listener)
        {
            Action<EventParam> thisEvent;
            if (instance.eventDictionary_param.TryGetValue(eventName, out thisEvent))
            {
                //Add more event to the existing one
                thisEvent += listener;

                //Update the Dictionary
                instance.eventDictionary_param[eventName] = thisEvent;
            }
            else
            {
                //Add event to the Dictionary for the first time
                thisEvent += listener;
                instance.eventDictionary_param.Add(eventName, thisEvent);
            }
        }

        public static void StopListening(string eventName, Action<EventParam> listener)
        {
            if (eventManager == null) return;
            Action<EventParam> thisEvent;
            if (instance.eventDictionary_param.TryGetValue(eventName, out thisEvent))
            {
                //Remove event from the existing one
                thisEvent -= listener;

                //Update the Dictionary
                instance.eventDictionary_param[eventName] = thisEvent;
            }
        }

        public static void TriggerEvent(string eventName, EventParam eventParam)
        {
            Action<EventParam> thisEvent = null;
            if (instance.eventDictionary_param.TryGetValue(eventName, out thisEvent))
            {
                thisEvent.Invoke(eventParam);
                // OR USE  instance.eventDictionary[eventName](eventParam);
            }
        }

        private static void placeTerrain()   { Option = PLACE; }
        private static void removeTerrain() { Option = REMOVE; }
        private static void placeTerrainParam(EventParam eventParam)
        { Option = PLACE_PARAM;  event_param = eventParam; }



         void Awake()
        {
            terrain = Terrain.activeTerrain;  
            Clear(terrain);
        }
        void Start()
        {
            // MassTreePlacement mtp = new MassTreePlacement();
            // PlaceRandomTrees(terrain, mtp);
        }

        // Start is called before the first frame update


        // Update is called once per frame
        void Update()
        {
            //Debug.Log("<color=yellow>update terrainManager"+Time.deltaTime+"</color>");
            switch (Option)
            {
                case PLACE:         PlaceTree();    Option = ORIGIN;    break;
                case REMOVE:        terrain = Terrain.activeTerrain; 
                                    Clear(terrain);    Option = ORIGIN;    break;
                case PLACE_PARAM:   PlaceTree(event_param);    Option = ORIGIN;     break;
                case ORIGIN:        return;
            }
        }
       public static void PlaceTree()   { PlaceRandomTrees(terrain, mtp);}
        public static void PlaceTree(EventParam eventParam) 
        {
            event_param = eventParam;
            terrain = Terrain.activeTerrain;
            mtp = new MassTreePlacement();
            PlaceRandomTrees(terrain, mtp, event_param);
        }

        public static void Clear(Terrain terrain)
        {
            if(terrain==null)
                return;
            else{
                terrain.terrainData.treeInstances = new TreeInstance[0];
                terrain.Flush();
            }
            //刷新更改
        }

        public static void PlaceRandomTrees(Terrain terrain, MassTreePlacement mtp)
        {
            var data = terrain.terrainData;

            var num = data.treePrototypes.Length;
            if (num == 0)
            {
                Debug.LogWarning("Can't place trees because no prototypes are defined. Process aborted.");
                return;
            }

            //Undo.RegisterCompleteObjectUndo(data, "Mass Place Trees");

            var start = DateTime.Now;

            var array = new TreeInstance[mtp.Count];
            var i = 0;
            var terrainScaleY = data.size.y;
            Debug.Log("terrainScaleY");
            Debug.Log(terrainScaleY);
            while (i < array.Length)
            {
                // stop if process have run for over X seconds
                var delta = DateTime.Now - start;
                if (delta.TotalSeconds >= mtp.MaxTime)
                {
                    Debug.LogWarning("Process was taking too much time to run");
                    return;
                }

                var position = new Vector3(Random.value, 0.0f, Random.value);

                // don't allow placement of trees below minWorldY and above maxWorldY
                var y = data.GetInterpolatedHeight(position.x, position.z);
                var worldY = y + terrain.transform.position.y;
/*
                var worldY = data.GetHeight(Convert.ToInt16(position.x*terrainScaleX),
                                                               Convert.ToInt16(position.z*terrainScaleZ));*/
                y =  y/terrainScaleY;
                position.y = y;

                if (worldY < mtp.MinWorldY || worldY > mtp.MaxWorldY)
                {
                    continue;
                }

                // don't allow placement of trees on surfaces flatter than minSlope and steeper than maxSlope
                var steepness = data.GetSteepness(position.x, position.z);
                if (steepness < mtp.MinSlope || steepness > mtp.MaxSlope)
                {
                    continue;
                }

                var color = Color.Lerp(Color.white, Color.gray * 0.7f, Random.value);
                color.a = 1f;

                var treeInstance = default(TreeInstance);
                treeInstance.position = position;
                treeInstance.color = color;
                treeInstance.lightmapColor = Color.white;
                treeInstance.prototypeIndex = Random.Range(0, num);
                treeInstance.widthScale = Random.Range(mtp.MinWidthScale, mtp.MaxWidthScale);
                treeInstance.heightScale = Random.Range(mtp.MinHeightScale, mtp.MaxHeightScale);
                array[i] = treeInstance;
                i++;
            }
            data.treeInstances = array;
            //RecalculateTreePositions(data);
            terrain.Flush();
        }

        public static void PlaceRandomTrees(Terrain terrain, MassTreePlacement mtp,EventParam eventParam)
        {
            var data = terrain.terrainData;
            var num = data.treePrototypes.Length;
            var terrainPosition = terrain.transform.position;
            if (num == 0)
            {
                Debug.LogWarning("Can't place trees because no prototypes are defined. Process aborted.");
                return;
            }

            //Undo.RegisterCompleteObjectUndo(data, "Mass Place Trees");

            var start = DateTime.Now;

            var treeMessage = eventParam.treeMessage;
            mtp.Count = (int)treeMessage.desity;
            var array = new TreeInstance[mtp.Count];
            var i = 0;
            var terrainScaleY = data.size.y;
            var terrainScaleX = data.size.x;
            var terrainScaleZ = data.size.z;
            Debug.Log("terrainScaleY");
            Debug.Log(terrainScaleY);
            while (i < array.Length)
            {
                // stop if process have run for over X seconds
                var delta = DateTime.Now - start;
                if (delta.TotalSeconds >= mtp.MaxTime)
                {
                    Debug.LogWarning("Process was taking too much time to run");
                    return;
                }

                float relativeX = treeMessage.bounding_origin[1] - terrainPosition.x;
                float relativeZ = treeMessage.bounding_origin[0]-terrainPosition.z;

                float startBoundingX = (relativeX - treeMessage.bounding_area[1]/2)/terrainScaleX;
                float endBoundingX = (relativeX + treeMessage.bounding_area[1] / 2) / terrainScaleX;

                float startBoundingZ = (relativeZ) / terrainScaleZ; //520/1000
                float endBoundingZ = (relativeZ + treeMessage.bounding_area[1]) / terrainScaleZ; //755/1000

                var position = new Vector3(Random.value*(endBoundingX- startBoundingX)+ startBoundingX,
                                                                0.0f, 
                                                            Random.value*(endBoundingZ-startBoundingZ)+startBoundingZ);

                // don't allow placement of trees below minWorldY and above maxWorldY
                var y = data.GetInterpolatedHeight(position.x, position.z);
                var worldY = y + terrain.transform.position.y;
                /*
                                var worldY = data.GetHeight(Convert.ToInt16(position.x*terrainScaleX),
                                                                               Convert.ToInt16(position.z*terrainScaleZ));*/
                y = y / terrainScaleY;
                position.y = y;

                if (worldY < mtp.MinWorldY || worldY > mtp.MaxWorldY)
                {
                    continue;
                }

                // don't allow placement of trees on surfaces flatter than minSlope and steeper than maxSlope
                var steepness = data.GetSteepness(position.x, position.z);
                if (steepness < mtp.MinSlope || steepness > mtp.MaxSlope)
                {
                    continue;
                }

                var color = Color.Lerp(Color.white, Color.gray * 0.7f, Random.value);
                color.a = 1f;

                var treeInstance = default(TreeInstance);
                treeInstance.position = position;
                treeInstance.color = color;
                treeInstance.lightmapColor = Color.white;
                treeInstance.prototypeIndex = Random.Range(0, num);
                treeInstance.widthScale = Random.Range(mtp.MinWidthScale, mtp.MaxWidthScale);
                treeInstance.heightScale = Random.Range(mtp.MinHeightScale, mtp.MaxHeightScale);
                array[i] = treeInstance;
                i++;
            }
            data.treeInstances = array;
            //RecalculateTreePositions(data);
            terrain.Flush();
        }




    }
}
