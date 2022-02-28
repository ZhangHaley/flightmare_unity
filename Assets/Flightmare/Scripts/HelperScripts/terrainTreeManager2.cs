
using System.Collections.Generic;
using System;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Threading;
using MessageSpec;


namespace RPGFlightmare
{
    // Idea from: https://www.tutorialfor.com/questions-7293.htm
    public class terrainTreeManager2 : MonoBehaviour
    {

        private static readonly Type terrainDataType = typeof(TerrainData);
        private static Terrain terrain;
        private static MassTreePlacement mtp = new MassTreePlacement();



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
            mtp.Count = (int)treeMessage.density;
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
                //x为roll 方向 z为 pitch方向，并且方向朝向为正z方向
                float relativeX = (float)treeMessage.bounding_origin[0] - terrainPosition.x;
                float relativeZ = (float)treeMessage.bounding_origin[1] - terrainPosition.z;

                float startBoundingX = (float)(relativeX - treeMessage.bounding_area[0]/2)/terrainScaleX;
                float endBoundingX = (float)(relativeX + treeMessage.bounding_area[0]/2) / terrainScaleX;

                float startBoundingZ = (float)(relativeZ - treeMessage.bounding_area[1]/2) / terrainScaleZ; 
                float endBoundingZ = (float)(relativeZ + treeMessage.bounding_area[1]/2) / terrainScaleZ;

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
