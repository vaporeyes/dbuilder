// ABOUTME: Ear-clipping triangulation vertex ported from UDB Source/Core/Geometry/EarClipVertex.cs.
// ABOUTME: Sidedef ref is opaque — carried along for downstream consumers, never inspected here.

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 */

using System;
using System.Collections.Generic;
using DBuilder.Geometry;

namespace DBuilder.Map;

public sealed class EarClipVertex
{
    private Vector2D pos;
    private Sidedef? sidedef;

    private LinkedListNode<EarClipVertex>? vertslink;
    private LinkedListNode<EarClipVertex>? reflexlink;
    private LinkedListNode<EarClipVertex>? eartiplink;

    public Vector2D Position => pos;
    internal LinkedListNode<EarClipVertex>? MainListNode => vertslink;
    public bool IsReflex => (reflexlink != null);
    public bool IsEarTip => (eartiplink != null);
    internal Sidedef? Sidedef { get => sidedef; set => sidedef = value; }

    // Copy constructor
    internal EarClipVertex(EarClipVertex v)
    {
        this.pos = v.pos;
        this.sidedef = v.sidedef;
        GC.SuppressFinalize(this);
    }

    // Copy constructor
    internal EarClipVertex(EarClipVertex v, Sidedef? sidedef)
    {
        this.pos = v.pos;
        this.sidedef = sidedef;
        GC.SuppressFinalize(this);
    }

    public EarClipVertex(Vector2D v, Sidedef? sidedef)
    {
        this.pos = v;
        this.sidedef = sidedef;
        GC.SuppressFinalize(this);
    }

    internal void Dispose()
    {
        reflexlink = null;
        eartiplink = null;
        vertslink = null;
        sidedef = null;
    }

    internal void SetVertsLink(LinkedListNode<EarClipVertex> link) => this.vertslink = link;

    // Removes the item from all lists
    internal void Remove()
    {
        vertslink!.List!.Remove(vertslink);
        if (reflexlink != null) reflexlink.List!.Remove(reflexlink);
        if (eartiplink != null) eartiplink.List!.Remove(eartiplink);
        reflexlink = null;
        eartiplink = null;
        vertslink = null;
    }

    public void AddReflex(LinkedList<EarClipVertex> reflexes)
    {
#if DEBUG
        if (vertslink == null) throw new Exception();
#endif
        if (reflexlink == null) reflexlink = reflexes.AddLast(this);
    }

    internal void RemoveReflex()
    {
        if (reflexlink != null) reflexlink.List!.Remove(reflexlink);
        reflexlink = null;
    }

    internal void AddEarTip(LinkedList<EarClipVertex> eartips)
    {
#if DEBUG
        if (vertslink == null) throw new Exception();
#endif
        if (eartiplink == null) eartiplink = eartips.AddLast(this);
    }

    internal void RemoveEarTip()
    {
        if (eartiplink != null) eartiplink.List!.Remove(eartiplink);
        eartiplink = null;
    }
}
