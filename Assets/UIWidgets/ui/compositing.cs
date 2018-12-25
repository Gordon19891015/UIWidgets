﻿using System;
using UIWidgets.flow;
using UnityEngine;

namespace UIWidgets.ui {
    public class SceneBuilder {
        private ContainerLayer _rootLayer;
        private ContainerLayer _currentLayer;

        public SceneBuilder() {
        }

        public Scene build() {
            return new Scene(this._rootLayer);
        }

        private void _pushLayer(ContainerLayer layer) {
            if (this._rootLayer == null) {
                this._rootLayer = layer;
                this._currentLayer = layer;
                return;
            }

            if (this._currentLayer == null) {
                return;
            }

            this._currentLayer.add(layer);
            this._currentLayer = layer;
        }

        public void pushTransform(Matrix4x4 matrix) {
            var layer = new TransformLayer();
            layer.transform = matrix.toMatrix3();
            this._pushLayer(layer);
        }

        public void pushClipRect(Rect clipRect) {
            var layer = new ClipRectLayer();
            layer.clipRect = clipRect;
            this._pushLayer(layer);
        }

        public void pushClipRRect(RRect clipRRect) {
            var layer = new ClipRRectLayer();
            layer.clipRRect = clipRRect;
            this._pushLayer(layer);
        }

        public void pushOpacity(int alpha) {
            var layer = new OpacityLayer();
            layer.alpha = alpha;
            this._pushLayer(layer);
        }

        public void pop() {
            if (this._currentLayer == null) {
                return;
            }

            this._currentLayer = this._currentLayer.parent;
        }

        public void addPicture(Offset offset, Picture picture,
            bool isComplex = false, bool willChange = false) {
            if (this._currentLayer == null) {
                return;
            }

            var layer = new PictureLayer();
            layer.offset = offset;
            layer.picture = picture;
            layer.isComplex = isComplex;
            layer.willChange = willChange;
            this._currentLayer.add(layer);
        }
    }

    public class Scene : IDisposable {
        public Scene(Layer rootLayer) {
            this._layerTree = new LayerTree();
            this._layerTree.rootLayer = rootLayer;
        }

        readonly LayerTree _layerTree;

        public LayerTree takeLayerTree() {
            return this._layerTree;
        }

        public void Dispose() {
        }
    }
}