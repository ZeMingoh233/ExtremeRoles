﻿using System;
using UnityEngine;

using ExtremeRoles.Helper;
using ExtremeRoles.Resources;
using ExtremeRoles.Performance;

namespace ExtremeRoles.Module.CustomMonoBehaviour;

[Il2CppRegister(
	new Type[]
	{
		typeof(IUsable)
	})]
public class PortalBase : MonoBehaviour
{

	public ImageNames UseIcon
	{
		get
		{
			return ImageNames.UseButton;
		}
	}

	public float UsableDistance
	{
		get
		{
			return 0.5f;
		}
	}

	public float PercentCool
	{
		get
		{
			return 0.0f;
		}
	}

	private SpriteRenderer img;
	private Vector3 pos;
	private PortalBase linkPortal = null;

	public PortalBase(IntPtr ptr) : base(ptr) { }

	public void Awake()
	{
		var collider = base.gameObject.AddComponent<CircleCollider2D>();
        collider.radius = 0.001f;
        collider.isTrigger = true;

        this.img = base.gameObject.AddComponent<SpriteRenderer>();
		this.linkPortal = null;

		this.img.sprite = Loader.CreateSpriteFromResources(
            Path.TeleporterNoneActivatePortal);
    }

	public static void Link(PortalBase a, PortalBase b)
    {
		a.SetTarget(b.gameObject.transform.position);
        b.SetTarget(a.gameObject.transform.position);

		a.img.sprite = a.GetSprite();
		b.img.sprite = b.GetSprite();

		a.linkPortal = b;
		b.linkPortal = a;
    }

	public void SetTarget(Vector3 pos)
	{
		this.pos = pos;
	}

	public float CanUse(
		GameData.PlayerInfo pc, out bool canUse, out bool couldUse)
	{
		float num = this.linkPortal ? 
			Vector2.Distance(
				pc.Object.GetTruePosition(),
				base.transform.position) : 
			float.MaxValue;

		couldUse = pc.IsDead ? false : true;
		canUse = (couldUse && num <= this.UsableDistance);
		return num;
	}

	public void SetOutline(bool on, bool mainTarget)
	{ }

	public void Use()
	{
        Player.RpcUncheckSnap(
			CachedPlayerControl.LocalPlayer.PlayerId, this.pos);
    }

	protected virtual Sprite GetSprite() => Loader.CreateSpriteFromResources(
		Path.TestButton);
}

public sealed class PortalFirst : PortalBase
{
	public PortalFirst(IntPtr ptr) : base(ptr) { }

	protected override Sprite GetSprite() => Loader.CreateSpriteFromResources(
		Path.TeleporterFirstPortal);
}

public sealed class PortalSecond : PortalBase
{
	public PortalSecond(IntPtr ptr) : base(ptr) { }

	protected override Sprite GetSprite() => Loader.CreateSpriteFromResources(
		Path.TeleporterSecondPortal);
}