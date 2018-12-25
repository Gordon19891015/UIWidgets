﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using UIWidgets.async;
using UIWidgets.foundation;
using UIWidgets.service;
using UIWidgets.rendering;
using UIWidgets.ui;
using UIWidgets.widgets;
using UnityEditor;
using UnityEngine;

namespace UIWidgets.editor {
    public abstract class UIWidgetsEditorWindow : EditorWindow {
        WindowAdapter _windowAdapter;

        void OnEnable() {
            if (this._windowAdapter == null) {
                this._windowAdapter = new WindowAdapter(this);
            }

            this._windowAdapter.OnEnable();

            var rootRenderBox = this.rootRenderBox();
            if (rootRenderBox != null) {
                this._windowAdapter.attachRootRenderBox(rootRenderBox);
                return;
            }

            this._windowAdapter.attachRootWidget(this.rootWidget());
        }

        void OnDisable() {
            this._windowAdapter.OnDisable();
        }

        void OnGUI() {
            this._windowAdapter.OnGUI();
        }

        void Update() {
            this._windowAdapter.Update();
        }

        protected virtual RenderBox rootRenderBox() {
            return null;
        }

        protected abstract Widget rootWidget();
    }

    public class WindowAdapter : Window {
        
        static readonly List<WindowAdapter> _windowAdapters = new List<WindowAdapter>();
        public static IEnumerable<WindowAdapter> windowAdapters {
            get { return _windowAdapters; }
        }

        public WindowAdapter(EditorWindow editorWindow)
        {
            this.editorWindow = editorWindow;
            this.editorWindow.wantsMouseMove = true;
            this.editorWindow.wantsMouseEnterLeaveWindow = true;

        }

        public readonly EditorWindow editorWindow;

        public WidgetInspectorService widgetInspectorService
        {
            get {
                D.assert(this._binding != null);
                return this._binding.widgetInspectorService;
            }
        }
        
        WidgetsBinding _binding;
        float _lastWindowWidth;
        float _lastWindowHeight;

        readonly DateTime _epoch = new DateTime(Stopwatch.GetTimestamp());
        readonly MicrotaskQueue _microtaskQueue = new MicrotaskQueue();
        readonly TimerProvider _timerProvider = new TimerProvider();
        readonly TextInput _textInput = new TextInput();
        readonly Rasterizer _rasterizer = new Rasterizer();

        bool _regenerateLayerTree;
        Surface _surface;
        
        bool _alive;

        public bool alive
        {
            get { return this._alive; }
        }

        public void OnEnable() {
            this._devicePixelRatio = EditorGUIUtility.pixelsPerPoint;
            this._lastWindowWidth = this.editorWindow.position.width;
            this._lastWindowHeight = this.editorWindow.position.height;
            this._physicalSize = new Size(
                this._lastWindowWidth * this._devicePixelRatio,
                this._lastWindowHeight * this._devicePixelRatio);

            D.assert(this._surface == null);
            this._surface = new EditorWindowSurface();

            this._rasterizer.setup(this._surface);
            
            _windowAdapters.Add(this);
            this._alive = true;
        }

        public void OnDisable() {
            _windowAdapters.Remove(this);
            this._alive = false;
            
            this._rasterizer.teardown();

            D.assert(this._surface != null);
            this._surface.Dispose();
            this._surface = null;
        }

        public override IDisposable getScope() {
            instance = this;
            if (this._binding == null) {
                this._binding = new WidgetsBinding();
            }

            WidgetsBinding.instance = this._binding;

            return new WindowDisposable(this);
        }

        class WindowDisposable : IDisposable {
            readonly WindowAdapter _window;

            public WindowDisposable(WindowAdapter window) {
                this._window = window;
            }
            
            public void Dispose() {
                D.assert(instance == this._window);
                instance = null;
                
                D.assert(WidgetsBinding.instance == this._window._binding);
                WidgetsBinding.instance = null;
            }
        }

        public void WithBinding(Action fn) {
            using (this.getScope()) {
                fn();
            }
        }
        
        public T WithBindingFunc<T>(Func<T> fn) {
            using (this.getScope()) {
                return fn();
            }
        }
        
        public void OnGUI() {
            using (this.getScope()) {
                bool dirty = false;

                if (this._devicePixelRatio != EditorGUIUtility.pixelsPerPoint) {
                    dirty = true;
                }

                if (this._lastWindowWidth != this.editorWindow.position.width
                    || this._lastWindowHeight != this.editorWindow.position.height) {
                    dirty = true;
                }

                if (dirty) {
                    this._devicePixelRatio = EditorGUIUtility.pixelsPerPoint;
                    this._lastWindowWidth = this.editorWindow.position.width;
                    this._lastWindowHeight = this.editorWindow.position.height;
                    this._physicalSize = new Size(
                        this._lastWindowWidth * this._devicePixelRatio,
                        this._lastWindowHeight * this._devicePixelRatio);

                    if (this.onMetricsChanged != null) {
                        this.onMetricsChanged();
                    }
                }

                this._doOnGUI();
            }
        }

        void _beginFrame() {
            if (this.onBeginFrame != null) {
                this.onBeginFrame(new DateTime(Stopwatch.GetTimestamp()) - this._epoch);
            }

            this.flushMicrotasks();

            if (this.onDrawFrame != null) {
                this.onDrawFrame();
            }
        }

        void _doOnGUI() {
            var evt = Event.current;

            if (evt.type == EventType.Repaint) {
                if (this._regenerateLayerTree) {
                    this._regenerateLayerTree = false;
                    this._beginFrame();
                } else {
                    this._rasterizer.drawLastLayerTree();
                }

                return;
            }

            if (this.onPointerEvent != null) {
                PointerData pointerData = null;

                if (evt.type == EventType.MouseDown) {
                    pointerData = new PointerData(
                        timeStamp: DateTime.Now,
                        change: PointerChange.down,
                        kind: PointerDeviceKind.mouse,
                        device: evt.button,
                        physicalX: evt.mousePosition.x * this._devicePixelRatio,
                        physicalY: evt.mousePosition.y * this._devicePixelRatio
                    );
                } else if (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp) {
                    pointerData = new PointerData(
                        timeStamp: DateTime.Now,
                        change: PointerChange.up,
                        kind: PointerDeviceKind.mouse,
                        device: evt.button,
                        physicalX: evt.mousePosition.x * this._devicePixelRatio,
                        physicalY: evt.mousePosition.y * this._devicePixelRatio
                    );
                } else if (evt.type == EventType.MouseDrag) {
                    pointerData = new PointerData(
                        timeStamp: DateTime.Now,
                        change: PointerChange.move,
                        kind: PointerDeviceKind.mouse,
                        device: evt.button,
                        physicalX: evt.mousePosition.x * this._devicePixelRatio,
                        physicalY: evt.mousePosition.y * this._devicePixelRatio
                    );
                } else if (evt.type == EventType.MouseMove) {
                    pointerData = new PointerData(
                        timeStamp: DateTime.Now,
                        change: PointerChange.hover,
                        kind: PointerDeviceKind.mouse,
                        device: evt.button,
                        physicalX: evt.mousePosition.x * this._devicePixelRatio,
                        physicalY: evt.mousePosition.y * this._devicePixelRatio
                    );
                }

                if (pointerData != null) {
                    this.onPointerEvent(new PointerDataPacket(new List<PointerData> {
                        pointerData
                    }));
                }
            }

            if (_textInput != null) {
                _textInput.OnGUI();
            }
        }

        public void Update() {
            Timer.update();
            
            using (this.getScope()) {
                this.doUpdate();
            }
        }

        private void doUpdate() {
            this.flushMicrotasks();
            this._timerProvider.update();
        }

        public override void scheduleFrame(bool regenerateLayerTree = true) {
            if (regenerateLayerTree) {
                this._regenerateLayerTree = true;
            }

            if (this.editorWindow != null) {
                this.editorWindow.Repaint();
            }
        }

        public override void render(Scene scene) {
            var layerTree = scene.takeLayerTree();
            if (layerTree == null) {
                return;
            }

            if (this._physicalSize.isEmpty) {
                return;
            }

            layerTree.frameSize = this._physicalSize;
            layerTree.devicePixelRatio = this._devicePixelRatio;
            this._rasterizer.draw(layerTree);
        }

        public override void scheduleMicrotask(Action callback) {
            this._microtaskQueue.scheduleMicrotask(callback);
        }

        public override void flushMicrotasks() {
            this._microtaskQueue.flushMicrotasks();
        }

        public override Timer run(TimeSpan duration, Action callback, bool periodic = false) {
            return periodic
                ? this._timerProvider.periodic(duration, callback)
                : this._timerProvider.run(duration, callback);
        }

        public void attachRootRenderBox(RenderBox root) {
            using (this.getScope()) {
                this._binding.renderView.child = root;
            }
        }

        public void attachRootWidget(Widget root) {
            using (this.getScope()) {
                this._binding.attachRootWidget(root);
            }
        }

        public override TextInput textInput {
            get { return this._textInput; }
        }
    }
}