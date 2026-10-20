// Anonymous SCORM 1.2 practice. No storage, network, identifiers or parent messages.
(() => {
  let initialized = false;
  let finished = false;
  let error = "0";
  const values = new Map([
    ["cmi.core.student_id", "anonymous"],
    ["cmi.core.student_name", "Anonymous practice"],
    ["cmi.core.lesson_status", "not attempted"],
    ["cmi.core.lesson_mode", "normal"],
    ["cmi.core.credit", "no-credit"],
    ["cmi.core.entry", "ab-initio"],
    ["cmi.core.total_time", "0000:00:00.00"],
    ["cmi.core.lesson_location", ""],
    ["cmi.core.score.raw", ""],
    ["cmi.core.score.min", "0"],
    ["cmi.core.score.max", "100"],
    ["cmi.core.exit", ""],
    ["cmi.core.session_time", "0000:00:00.00"],
    ["cmi.suspend_data", ""],
    ["cmi.launch_data", ""],
    ["cmi.student_data.mastery_score", "80"],
    ["cmi.student_data.max_time_allowed", ""],
    ["cmi.student_data.time_limit_action", "continue,no message"],
    ["cmi.student_preference.audio", "0"],
    ["cmi.student_preference.language", ""],
    ["cmi.student_preference.speed", "0"],
    ["cmi.student_preference.text", "0"],
    ["cmi.comments", ""],
    ["cmi.comments_from_lms", ""],
  ]);
  const readOnly = /^(cmi\.core\.(student_id|student_name|lesson_mode|credit|entry|total_time)|cmi\.launch_data|cmi\.student_data\..*|cmi\.comments_from_lms)$/;
  const arrayKey = /^cmi\.(interactions|objectives)\.([0-9]{1,2})\.(id|type|time|student_response|result|weighting|latency|status|score\.(raw|min|max)|objectives\.0\.id|correct_responses\.0\.pattern)$/;
  const fail = (code, value = "false") => { error = code; return value; };
  const ok = (value = "true") => { error = "0"; return value; };
  const ready = () => initialized && !finished;
  const api = {
    LMSInitialize(arg) {
      if (arg !== "") return fail("201");
      if (initialized || finished) return fail("101");
      initialized = true; return ok();
    },
    LMSFinish(arg) {
      if (!ready()) return fail("301");
      if (arg !== "") return fail("201");
      finished = true; values.clear(); return ok();
    },
    LMSGetValue(key) {
      if (!ready()) return fail("301", "");
      if (key === "cmi.core._children") return ok("student_id,student_name,lesson_location,credit,lesson_status,entry,score,total_time,lesson_mode,exit,session_time");
      if (key === "cmi.core.score._children") return ok("raw,min,max");
      if (key === "cmi.interactions._children") return ok("id,objectives,time,type,correct_responses,weighting,student_response,result,latency");
      if (key === "cmi.objectives._children") return ok("id,score,status");
      if (/^cmi\.(interactions|objectives)\._count$/.test(key)) {
        const prefix = key.replace("_count", "");
        const indices = [...values.keys()].filter(k => k.startsWith(prefix)).map(k => Number(k.slice(prefix.length).split(".")[0]));
        return ok(String(indices.length ? Math.max(...indices) + 1 : 0));
      }
      if (key === "cmi.core.exit" || key === "cmi.core.session_time") return fail("404", "");
      if (!values.has(key) && !arrayKey.test(key)) return fail("401", "");
      return ok(values.get(key) || "");
    },
    LMSSetValue(key, value) {
      if (!ready()) return fail("301");
      if (readOnly.test(key) || key.endsWith("._children") || key.endsWith("._count")) return fail("403");
      if (!values.has(key) && !arrayKey.test(key)) return fail("401");
      if (typeof value !== "string" || value.length > 4096 || (!values.has(key) && values.size >= 256)) return fail("405");
      if (key === "cmi.core.lesson_status" && !["passed", "completed", "failed", "incomplete", "browsed", "not attempted"].includes(value)) return fail("405");
      values.set(key, value); return ok();
    },
    LMSCommit(arg) { return !ready() ? fail("301") : arg !== "" ? fail("201") : ok(); },
    LMSGetLastError() { return error; },
    LMSGetErrorString(code) { return ({"0":"No error","101":"General exception","201":"Invalid argument","301":"Not initialized","401":"Not implemented","403":"Read only","404":"Write only","405":"Incorrect data type"})[String(code)] || "Unknown error"; },
    LMSGetDiagnostic(code) { return api.LMSGetErrorString(code || error); },
  };
  Object.defineProperty(window, "API", { value: Object.freeze(api), writable: false, configurable: false });
  // Older Storyline drivers search a cross-origin parent before the current window.
  // Bind only this API-discovery hook after an external driver loads, before page load.
  document.addEventListener("load", event => {
    if (event.target instanceof HTMLScriptElement && typeof window.SCORM_GetAPI === "function")
      window.SCORM_GetAPI = () => api;
  }, true);
})();
