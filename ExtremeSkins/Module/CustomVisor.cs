﻿using System.IO;
using System.Text;

using UnityEngine;

using ExtremeSkins.Core.ExtremeVisor;
using ExtremeSkins.Module.Interface;
using ExtremeRoles.Performance;

namespace ExtremeSkins.Module;

#if WITHVISOR
public sealed class CustomVisor : ICustomCosmicData<VisorData>
{
    public VisorData Data
    {
        get => this.visor;
    }

    public string Author
    {
        get => this.author;
    }
    public string Name
    {
        get => this.name;
    }

    public string Id
    {
        get => $"visor_{new DirectoryInfo(this.folderPath).Name}_{this.author}_{this.name}";
    }

    private VisorData visor;

    private string name;
    private string author;
    private string folderPath;

    private bool isBehindHat;
    private bool hasShader;
    private bool hasLeftImg;

    public CustomVisor(
        string folderPath,
        VisorInfo info)
    {
        this.folderPath = folderPath;
        this.author = info.Author;
        this.name = info.Name;

        this.isBehindHat = info.BehindHat;
        this.hasLeftImg = info.LeftIdle;
        this.hasShader = info.Shader;
    }

    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        builder
            .AppendLine($" - Name      : {this.name}")
            .AppendLine($" - Author    : {this.author}")
            .AppendLine($" - Load from : {this.folderPath}")
            .Append    ($" - Id        : {this.Id}");

        return builder.ToString();
    }

    public VisorData GetData()
    {
        if (this.visor != null) { return this.visor; }

        this.visor = ScriptableObject.CreateInstance<VisorData>();
        this.visor.name = Helper.Translation.GetString(this.Name);
        this.visor.displayOrder = 99;
        this.visor.ProductId = this.Id;
        this.visor.ChipOffset = new Vector2(0f, 0.2f);
        this.visor.Free = true;
        this.visor.NotInStore = true;

        // 256×144の画像
        this.visor.viewData.viewData = ScriptableObject.CreateInstance<VisorViewData>();
        this.visor.viewData.viewData.IdleFrame = loadVisorSprite(
            Path.Combine(this.folderPath, DataStructure.IdleImageName));

        if (this.hasLeftImg)
        {
            this.visor.viewData.viewData.LeftIdleFrame = loadVisorSprite(
                Path.Combine(this.folderPath, DataStructure.FlipIdleImageName));
        }
        if (this.hasShader)
        {
            Material altShader = new Material(
                FastDestroyableSingleton<HatManager>.Instance.PlayerMaterial);
            altShader.shader = Shader.Find("Unlit/PlayerShader");

            this.visor.viewData.viewData.AltShader = altShader;
        }

        this.visor.behindHats = this.isBehindHat;

        return this.visor;

    }

    private Sprite loadVisorSprite(
        string path)
    {
        Texture2D texture = Loader.LoadTextureFromDisk(path);
        if (texture == null)
        {
            return null;
        }
        Sprite sprite = Sprite.Create(
            texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.53f, 0.575f), texture.width * 0.375f);
        if (sprite == null)
        {
            return null;
        }
        texture.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        sprite.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }
}
#endif
