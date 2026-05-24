using System;
using System.Collections.Generic;
using LeiTing.Core;
using LeiTing.Storage;
using UnityEngine;

namespace LeiTing.UI
{
    public class PlaneManager : MonoSingleton<PlaneManager>
    {
        private const string SelectedPlaneKey = "leiting_selected_plane";
        private const string OwnedPlaneKeyPrefix = "leiting_plane_owned_";
        private const string PlaneAdKeyPrefix = "leiting_plane_ad_";
        private const string PlaneIconFolder = "Assets/Art/Sprites/UI/Player";

        private readonly List<PlaneData> planes = new List<PlaneData>();

        public event Action OnPlaneDataChanged;

        public static PlaneManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindObjectOfType<PlaneManager>();
            if (existing != null)
            {
                return existing;
            }

            var managerObject = new GameObject("PlaneManager");
            return managerObject.AddComponent<PlaneManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
                EnsureDefaultPlanes();
            }
        }

        public IReadOnlyList<PlaneData> GetPlanes()
        {
            EnsureDefaultPlanes();

            var result = new List<PlaneData>(planes.Count);
            foreach (var plane in planes)
            {
                result.Add(ClonePlane(plane));
            }

            return result;
        }

        public PlaneData GetSelectedPlane()
        {
            EnsureDefaultPlanes();
            var selectedId = GameStorage.GetInt(SelectedPlaneKey, 1);

            foreach (var plane in planes)
            {
                if (plane.id == selectedId && plane.owned)
                {
                    return ClonePlane(plane);
                }
            }

            return planes.Count > 0 ? ClonePlane(planes[0]) : null;
        }

        public PlaneData GetPlane(int planeId)
        {
            EnsureDefaultPlanes();

            foreach (var plane in planes)
            {
                if (plane.id == planeId)
                {
                    return ClonePlane(plane);
                }
            }

            return null;
        }

        public void SelectPlane(int planeId)
        {
            EnsureDefaultPlanes();
            var plane = FindPlane(planeId);

            if (plane == null || !plane.owned)
            {
                return;
            }

            GameStorage.SetInt(SelectedPlaneKey, planeId);
            RefreshSelection();
            GameStorage.Save();
            OnPlaneDataChanged?.Invoke();
        }

        public void AddAdProgress(int planeId)
        {
            EnsureDefaultPlanes();
            var plane = FindPlane(planeId);

            if (plane == null || plane.owned)
            {
                return;
            }

            plane.adCountWatched = Mathf.Min(plane.adCountRequired, plane.adCountWatched + 1);
            GameStorage.SetInt(GetAdKey(planeId), plane.adCountWatched);

            if (plane.adCountWatched >= plane.adCountRequired)
            {
                UnlockPlane(planeId);
            }
            else
            {
                GameStorage.Save();
                OnPlaneDataChanged?.Invoke();
            }
        }

        public bool IsPlaneUnlocked(int planeId)
        {
            EnsureDefaultPlanes();
            var plane = FindPlane(planeId);
            return plane != null && plane.owned;
        }

        private void UnlockPlane(int planeId)
        {
            var plane = FindPlane(planeId);

            if (plane == null)
            {
                return;
            }

            plane.owned = true;
            GameStorage.SetInt(GetOwnedKey(planeId), 1);
            GameStorage.SetInt(SelectedPlaneKey, planeId);
            RefreshSelection();
            GameStorage.Save();
            OnPlaneDataChanged?.Invoke();
        }

        private void EnsureDefaultPlanes()
        {
            if (planes.Count > 0)
            {
                RefreshSelection();
                return;
            }

            planes.Add(CreatePlane(1, "雷霆一号", "Assets/Prefabs/Player/warplane-01.prefab", 100, 12, 8f, 7.2f, true, 0));
            planes.Add(CreatePlane(2, "星火翼", "Assets/Prefabs/Player/warplane-01.prefab", 125, 15, 7.5f, 6.8f, false, 3));
            planes.Add(CreatePlane(3, "苍蓝矛", "Assets/Prefabs/Player/warplane-01.prefab", 90, 20, 9.5f, 8.3f, false, 5));
            RefreshSelection();
        }

        private PlaneData CreatePlane(
            int id,
            string name,
            string prefabPath,
            int hp,
            int attack,
            float fireRate,
            float moveSpeed,
            bool defaultOwned,
            int adCountRequired)
        {
            var owned = defaultOwned || GameStorage.GetInt(GetOwnedKey(id), defaultOwned ? 1 : 0) == 1;
            return new PlaneData
            {
                id = id,
                name = name,
                iconPath = GetPlaneIconPath(prefabPath),
                prefabPath = prefabPath,
                hp = hp,
                attack = attack,
                fireRate = fireRate,
                moveSpeed = moveSpeed,
                owned = owned,
                selected = false,
                unlockType = owned ? PlaneUnlockType.Default : PlaneUnlockType.Ad,
                adCountRequired = Mathf.Max(1, adCountRequired),
                adCountWatched = GameStorage.GetInt(GetAdKey(id), 0)
            };
        }

        private PlaneData FindPlane(int planeId)
        {
            foreach (var plane in planes)
            {
                if (plane.id == planeId)
                {
                    return plane;
                }
            }

            return null;
        }

        private void RefreshSelection()
        {
            if (planes.Count == 0)
            {
                return;
            }

            var selectedId = GameStorage.GetInt(SelectedPlaneKey, 1);
            var selectedPlane = FindPlane(selectedId);

            if (selectedPlane == null || !selectedPlane.owned)
            {
                selectedId = planes[0].id;
                GameStorage.SetInt(SelectedPlaneKey, selectedId);
            }

            foreach (var plane in planes)
            {
                plane.selected = plane.id == selectedId;
                plane.owned = plane.owned || GameStorage.GetInt(GetOwnedKey(plane.id), plane.owned ? 1 : 0) == 1;
                plane.adCountWatched = GameStorage.GetInt(GetAdKey(plane.id), plane.adCountWatched);
            }
        }

        private static PlaneData ClonePlane(PlaneData source)
        {
            return new PlaneData
            {
                id = source.id,
                name = source.name,
                iconPath = source.iconPath,
                prefabPath = source.prefabPath,
                hp = source.hp,
                attack = source.attack,
                fireRate = source.fireRate,
                moveSpeed = source.moveSpeed,
                owned = source.owned,
                selected = source.selected,
                unlockType = source.unlockType,
                adCountRequired = source.adCountRequired,
                adCountWatched = source.adCountWatched
            };
        }

        private static string GetPlaneIconPath(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                return string.Empty;
            }

            var normalizedPath = prefabPath.Replace("\\", "/").Trim();
            var fileNameStart = normalizedPath.LastIndexOf('/') + 1;
            var fileName = normalizedPath.Substring(fileNameStart);
            var extensionStart = fileName.LastIndexOf('.');
            var planeName = extensionStart >= 0 ? fileName.Substring(0, extensionStart) : fileName;

            return string.IsNullOrEmpty(planeName) ? string.Empty : $"{PlaneIconFolder}/{planeName}.png";
        }

        private static string GetOwnedKey(int planeId)
        {
            return OwnedPlaneKeyPrefix + planeId;
        }

        private static string GetAdKey(int planeId)
        {
            return PlaneAdKeyPrefix + planeId;
        }
    }
}
