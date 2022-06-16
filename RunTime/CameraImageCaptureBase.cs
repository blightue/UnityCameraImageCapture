﻿using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using SuiSuiShou.CIC.Data;

namespace SuiSuiShou.CIC.Core
{
    public abstract class CameraImageCaptureBase : MonoBehaviour
    {
        #region Properties
        public abstract Vector2Int ImageResolution { get; set; }

        public abstract WriteFileType WriteType { get; set; }
        public abstract ImageFormat ImageFormat { get; set; }

        public abstract bool IsOverrideFile { get; set; }
        public abstract bool IsImageSerial  { get; set; }

        public abstract string SaveFolderPath { get; set; }
        public abstract string FileName { get; set; }

        #endregion

        public Dictionary<string, FileInfor> fileInfors = new Dictionary<string, FileInfor>();
        public Camera targetCamera;


        public void Reset()
        {
            targetCamera = Camera.main;
            SaveFolderPath = Application.persistentDataPath;
            ImageFormat = ImageFormat.png;
            IsOverrideFile = false;
            IsImageSerial = true;
            fileInfors = CaptureInforManager.ReadLocalData();
            FileName = "cameraCaptures";
            Debug.Log("Reset CIC");
        }

        private void OnDisable()
        {
#if !UNITY_EDITOR
            CaptureInforManager.WriteLocalData(fileInfors);
#endif
        }

        private void OnEnable()
        {
#if !UNITY_EDITOR
            fileInfors = CaptureInforManager.ReadLocalData();
#endif
        }

        public void CaptureAndSaveImage()
        {
            StartSaveImage(SaveFolderPath, FileName, CaptureImage(targetCamera, ImageResolution, 8));
        }

        public Texture2D CaptureImage(Camera camera, Vector2Int resolution, int depth)
        {
            if (!CameraCheck(camera)) return null;
            RenderTexture cameraTexture = new RenderTexture(resolution.x, resolution.y, depth);

            camera.targetTexture = cameraTexture;
            camera.Render();

            RenderTexture.active = cameraTexture;

            Debug.Log("Camera rect = " + camera.pixelWidth + " - " + camera.pixelHeight
                + "  Screen resolution = " + Screen.width + " - " + Screen.height);
            Texture2D texture2D = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false);

            texture2D.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0);

            RenderTexture.active = null;
            camera.targetTexture = null;

            return texture2D;
        }

        public void StartSaveImage(string folderPath, string fileName, Texture2D texture)
        {
            if (!FolderPathCheck(folderPath)) return;
            fileName = UpdateFileName(fileName);
            if (!FileNameCheck(fileName)) return;

            byte[] saveData = null;
            switch (ImageFormat)
            {
                //case ImageFormat.exr:
                //    saveData = texture.EncodeToEXR();
                //    break;
                case ImageFormat.jpg:
                    saveData = texture.EncodeToJPG();
                    break;

                case ImageFormat.png:
                    saveData = texture.EncodeToPNG();
                    break;

                case ImageFormat.tga:
                    saveData = texture.EncodeToTGA();
                    break;
            }
            DestroyImmediate(texture);

            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning(string.Format("Folder path {0} do not exist"));
                return;
            }
            string fullpath;
            if (IsImageSerial)
                fullpath = Path.Combine(folderPath, fileName + "-" + fileInfors[fileName].fileCount + '.' + ImageFormat.ToString());
            else
                fullpath = Path.Combine(folderPath, fileName + '.' + ImageFormat.ToString());
            fileInfors[fileName].fileCount++;

            switch (WriteType)
            {
                case WriteFileType.MainThread:
                    DataSaver.WriteDataMain(fullpath, saveData);
                    break;

                case WriteFileType.Async:
                    DataSaver.WriteDataTask(fullpath, saveData);
                    break;
                    //case WriteFileType.JobSystem:
                    //    DataSaver.WriteDataJobS(fullpath, saveData);
                    //    break;
            }

            Debug.Log("Capture image save to " + fullpath);
        }

        private string UpdateFileName(string name)
        {
            if (!fileInfors.ContainsKey(name))
            {
                if (IsOverrideFile)
                {
                    fileInfors[name] = new FileInfor(SaveFolderPath, 0);
                    return name;
                }
                while
                    (
                    PlayerPrefs.HasKey("CICFileCount" + name) &&
                    File.Exists(Path.Combine(SaveFolderPath, name + "-0.png"))
                    )
                {
                    name += "-New";
                }
                fileInfors[name] = new FileInfor(SaveFolderPath, 0);
                //Debug.Log(fileInfors[name].fileCount + fileInfors.ContainsKey(name).ToString());
            }
            FileName = name;
            return name;
        }

        private bool CameraCheck(Camera camera)
        {
            if (camera == null)
            {
                Debug.LogWarning("Camera is null");
                return false;
            }
            return true;
        }

        private bool FolderPathCheck(string path)
        {
            if (path == "" || path == null)
            {
                Debug.LogWarning("Savepath is null or empty");
                return false;
            }
            if (!Directory.Exists(path))
            {
                Debug.LogWarning("Savepath do not exist");
                return false;
            }
            return true;
        }

        private bool FileNameCheck(string fileName)
        {
            if (fileName == "" || fileName == null)
            {
                Debug.LogWarning("fileName is null or empty");
                return false;
            }
            return true;
        }

        private void OnApplicationQuit()
        {
            if (fileInfors.Count > 0)
            {
                foreach (var item in fileInfors)
                {
                    PlayerPrefs.SetInt("CICFileCount" + item.Key, item.Value.fileCount);
                    PlayerPrefs.SetString("CICFileFolder" + item.Key, item.Value.folderPath);
                }
                PlayerPrefs.Save();
            }
        }
    }
}