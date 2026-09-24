mergeInto(LibraryManager.library, {
  ScaffoldAnalyticsGetWebGlCrashReportLength: function () {
    var report = ScaffoldAnalyticsWebGlDiagnostics.readReport();
    window.__scaffoldAnalyticsLastReportJson = report;
    return lengthBytesUTF8(report);
  },

  ScaffoldAnalyticsCopyWebGlCrashReport: function (buffer, capacity) {
    var report = window.__scaffoldAnalyticsLastReportJson || ScaffoldAnalyticsWebGlDiagnostics.readReport();
    stringToUTF8(report, buffer, capacity);
    window.__scaffoldAnalyticsLastReportJson = null;
  },

  ScaffoldAnalyticsAcknowledgeWebGlCrashReport: function (sessionIdPointer) {
    ScaffoldAnalyticsWebGlDiagnostics.acknowledge(UTF8ToString(sessionIdPointer));
  },

  ScaffoldAnalyticsSetWebGlCheckpoint: function (phasePointer, detailsPointer) {
    ScaffoldAnalyticsWebGlDiagnostics.setCheckpoint(
      UTF8ToString(phasePointer),
      UTF8ToString(detailsPointer));
  }
});

var ScaffoldAnalyticsWebGlDiagnostics = {
  stateKey: "scaffold.analytics.webgl.session.v1",
  acknowledgementKey: "scaffold.analytics.webgl.ack.v1",
  initialized: false,
  previous: null,
  current: null,

  readReport: function () {
    try {
      if (window.gearEngineDiagnostics && typeof window.gearEngineDiagnostics.getReport === "function") {
        var hostReport = window.gearEngineDiagnostics.getReport();
        return JSON.stringify(this.applyAcknowledgement(hostReport));
      }

      this.ensureFallbackSession();
      return JSON.stringify({
        current: this.current,
        previous: this.previous,
        previousEndedAbruptly: Boolean(
          this.previous &&
          !this.previous.cleanExit &&
          localStorage.getItem(this.acknowledgementKey) !== this.previous.sessionId)
      });
    } catch (error) {
      console.error("[Scaffold Analytics] Unable to read WebGL diagnostics.", error);
      return "";
    }
  },

  acknowledge: function (sessionId) {
    try {
      if (sessionId) {
        localStorage.setItem(this.acknowledgementKey, sessionId);
      }
    } catch (error) {
      console.error("[Scaffold Analytics] Unable to acknowledge WebGL diagnostics.", error);
    }
  },

  setCheckpoint: function (phase, details) {
    try {
      if (window.gearEngineDiagnostics && typeof window.gearEngineDiagnostics.markPhase === "function") {
        window.gearEngineDiagnostics.markPhase(phase, details ? { message: details } : null);
        return;
      }

      this.ensureFallbackSession();
      this.persistFallbackCheckpoint(phase, details);
    } catch (error) {
      console.error("[Scaffold Analytics] Unable to persist a WebGL checkpoint.", error);
    }
  },

  applyAcknowledgement: function (report) {
    if (!report || !report.previous) {
      return report;
    }

    if (localStorage.getItem(this.acknowledgementKey) === report.previous.sessionId) {
      report.previousEndedAbruptly = false;
    }

    return report;
  },

  ensureFallbackSession: function () {
    if (this.initialized) {
      return;
    }

    this.initialized = true;
    this.previous = this.readStoredSession();
    this.current = {
      sessionId: Date.now().toString(36) + "-" + Math.random().toString(36).slice(2, 10),
      startedAt: Date.now(),
      updatedAt: Date.now(),
      phase: "unity_runtime_ready",
      cleanExit: false,
      runtimeReady: true,
      events: [],
      environment: this.captureEnvironment()
    };
    this.persistFallbackCheckpoint("unity_runtime_ready", "Scaffold runtime bridge initialized.");
    this.installFallbackHooks();
  },

  readStoredSession: function () {
    try {
      var stored = localStorage.getItem(this.stateKey);
      return stored ? JSON.parse(stored) : null;
    } catch (error) {
      console.error("[Scaffold Analytics] Unable to parse the previous WebGL session.", error);
      return null;
    }
  },

  persistFallbackCheckpoint: function (phase, details, cleanExit) {
    if (!this.current) {
      return;
    }

    this.current.phase = phase;
    this.current.updatedAt = Date.now();
    this.current.cleanExit = Boolean(cleanExit);
    this.current.events.push({
      at: this.current.updatedAt,
      phase: phase,
      details: details ? { message: String(details).slice(0, 100) } : null
    });
    this.current.events = this.current.events.slice(-24);
    localStorage.setItem(this.stateKey, JSON.stringify(this.current));
  },

  installFallbackHooks: function () {
    var self = this;
    window.addEventListener("error", function (event) {
      self.persistFallbackCheckpoint("javascript_error", event.message || "Unknown JavaScript error.");
    });
    window.addEventListener("unhandledrejection", function (event) {
      self.persistFallbackCheckpoint("unhandled_rejection", String(event.reason || "Unhandled promise rejection."));
    });
    window.addEventListener("pagehide", function () {
      self.persistFallbackCheckpoint("page_hidden", "", true);
    });
    window.addEventListener("beforeunload", function () {
      self.persistFallbackCheckpoint("page_unloaded", "", true);
    });
  },

  captureEnvironment: function () {
    var heapBytes = 0;
    if (typeof HEAP8 !== "undefined" && HEAP8 && HEAP8.buffer) {
      heapBytes = HEAP8.buffer.byteLength || 0;
    }

    return {
      userAgent: navigator.userAgent || "",
      platform: (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || "",
      viewport: window.innerWidth + "x" + window.innerHeight,
      devicePixelRatio: window.devicePixelRatio || 1,
      wasmHeapBytes: heapBytes
    };
  }
};
