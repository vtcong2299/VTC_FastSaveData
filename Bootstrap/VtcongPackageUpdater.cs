#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace Vtcong.Bootstrap
{
    // bản sao giống hệt nằm trong Bootstrap của mọi package com.vtcong.*; mỗi phiên editor chỉ một bản chạy
    [InitializeOnLoad]
    internal static class VtcongPackageUpdater
    {
        private const string Owner = "vtcong2299";
        private const string SessionKey = "vtcong.updater.checked";
        private const string SkipKeyPrefix = "vtcong.updater.skip.";
        private const string Title = "VTC Packages";

        // bản thấp hơn baseline được tự nâng lên bản mới nhất; bản từ baseline trở lên chỉ hỏi người dùng
        // luôn cài bằng git URL không gắn tag: gắn tag thì nút Update của Package Manager chỉ tải lại đúng tag đó
        private static readonly (string Id, string Repo, string Baseline)[] Packages =
        {
            ("com.vtcong.singleton", "VTC_Singleton", "1.1.0"),
            ("com.vtcong.core", "VTC_Core-Naba-", "1.1.0"),
            ("com.vtcong.custombehaviour", "VTC_CustomBehavior", "1.1.0"),
            ("com.vtcong.fastpool", "VTC_FastPool", "1.1.0"),
            ("com.vtcong.fastsave", "VTC_FastSaveData", "1.1.0"),
        };

        private static ListRequest _list;
        private static readonly List<(string Id, string Repo, Version Installed, UnityWebRequest Request)> Checks = new();

        static VtcongPackageUpdater()
        {
            EditorApplication.delayCall += Begin;
        }

        private static void Begin()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            _list = Client.List(true, false);
            EditorApplication.update += PollList;
        }

        private static void PollList()
        {
            if (_list == null || !_list.IsCompleted) return;
            EditorApplication.update -= PollList;
            var result = _list.Status == StatusCode.Success ? _list.Result.ToArray() : Array.Empty<UnityEditor.PackageManager.PackageInfo>();
            _list = null;

            var lift = new List<string>();
            foreach (var info in result)
            {
                if (info.source != PackageSource.Git) continue;
                var package = Packages.FirstOrDefault(p => p.Id == info.name);
                if (package.Id == null || !TryParse(info.version, out var installed)) continue;

                if (installed < Version.Parse(package.Baseline))
                {
                    lift.Add(GitUrl(package.Repo));
                    continue;
                }

                // query string để không nhận bản package.json cũ còn nằm trong cache của raw.githubusercontent
                var request = UnityWebRequest.Get($"https://raw.githubusercontent.com/{Owner}/{package.Repo}/HEAD/package.json?t={DateTime.UtcNow.Ticks}");
                request.timeout = 10;
                request.SendWebRequest();
                Checks.Add((package.Id, package.Repo, installed, request));
            }

            if (lift.Count > 0)
            {
                foreach (var check in Checks) check.Request.Dispose();
                Checks.Clear();
                Debug.Log($"[{Title}] Bản thấp hơn bản tối thiểu, nâng lên bản mới nhất:{string.Join(", ", lift)}");
                Client.AddAndRemove(lift.ToArray());
                return;
            }

            if (Checks.Count > 0) EditorApplication.update += PollRemote;
        }

        private static void PollRemote()
        {
            if (Checks.Any(c => !c.Request.isDone)) return;
            EditorApplication.update -= PollRemote;

            var updates = new List<(string Id, string Repo, Version Installed, Version Latest)>();
            foreach (var check in Checks)
            {
                if (check.Request.result == UnityWebRequest.Result.Success
                    && TryParse(Regex.Match(check.Request.downloadHandler.text, "\"version\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value, out var latest)
                    && latest > check.Installed
                    && EditorPrefs.GetString(SkipKeyPrefix + check.Id) != latest.ToString())
                    updates.Add((check.Id, check.Repo, check.Installed, latest));
                check.Request.Dispose();
            }
            Checks.Clear();

            if (updates.Count == 0) return;

            var lines = string.Join("\n", updates.Select(u => $"{u.Id}: {u.Installed} -> {u.Latest}"));
            var choice = EditorUtility.DisplayDialogComplex(Title, $"Có bản mới cho package:\n\n{lines}", "Cập nhật", "Để sau", "Bỏ qua bản này");
            if (choice == 0)
            {
                Debug.Log($"[{Title}] Đang cập nhật:\n{lines}");
                Client.AddAndRemove(updates.Select(u => GitUrl(u.Repo)).ToArray());
            }
            else if (choice == 2)
                foreach (var update in updates)
                    EditorPrefs.SetString(SkipKeyPrefix + update.Id, update.Latest.ToString());
        }

        private static string GitUrl(string repo) => $"https://github.com/{Owner}/{repo}.git";

        private static bool TryParse(string version, out Version parsed) =>
            Version.TryParse((version ?? string.Empty).Split('-', '+')[0], out parsed);
    }
}
#endif
