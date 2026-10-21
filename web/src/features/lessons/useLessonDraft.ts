import { useCallback, useEffect, useRef, useState } from "react";
import { api, ApiError } from "../../api/client";
import type { Detail, LessonDocument } from "./types";
import { lessonBase } from "./types";
import { useQueryClient } from "@tanstack/react-query";

export function useLessonDraft(initial: Detail, organisation: string) {
  const client = useQueryClient();
  const [server, setServer] = useState(initial);
  const [document, setDocument] = useState(initial.document);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<unknown>();
  const [blocked, setBlocked] = useState(false);
  const current = useRef(initial.document);
  const saved = useRef(initial);
  const flight = useRef<Promise<Detail> | null>(null);
  const mounted = useRef(true);
  const changed = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const update = useCallback(
    (change: (value: LessonDocument) => LessonDocument) => {
      current.current = change(current.current);
      changed.current = true;
      setDocument(current.current);
      setDirty(true);
      setBlocked(false);
      setError(undefined);
    },
    [],
  );
  const adopt = useCallback((value: Detail) => {
    saved.current = value;
    current.current = value.document;
    changed.current = false;
    if (mounted.current) {
      setServer(value);
      setDocument(value.document);
      setDirty(false);
      setBlocked(false);
      setError(undefined);
    }
  }, []);
  useEffect(() => {
    if (
      !changed.current &&
      !flight.current &&
      initial.lesson.revision > saved.current.lesson.revision
    )
      adopt(initial);
  }, [initial, adopt]);
  const save = useCallback(
    async function saveLatest(): Promise<Detail> {
      if (flight.current) {
        await flight.current;
        return changed.current ? saveLatest() : saved.current;
      }
      if (!changed.current) return saved.current;
      const snapshot = current.current;
      const fingerprint = JSON.stringify(snapshot);
      const expected = saved.current.lesson.revision;
      setSaving(true);
      setError(undefined);
      const pending = api<Detail>(
        lessonBase(organisation) + "/" + saved.current.lesson.id,
        {
          method: "PUT",
          body: {
            expectedRevision: expected,
            setup: snapshot.setup,
            sections: snapshot.sections,
            ministryForm: snapshot.ministryForm,
            reason: "Teacher edit / autosave",
          },
        },
      );
      flight.current = pending;
      try {
        const result = await pending;
        saved.current = result;
        client.setQueryData(["lesson", organisation, result.lesson.id], result);
        void client.invalidateQueries({ queryKey: ["lessons", organisation] });
        void client.invalidateQueries({
          queryKey: ["teacher-home-drafts", organisation],
        });
        if (mounted.current) {
          setServer(result);
          if (JSON.stringify(current.current) === fingerprint) {
            current.current = result.document;
            changed.current = false;
            setDocument(result.document);
            setDirty(false);
          }
          setBlocked(false);
        }
        return result;
      } catch (failure) {
        if (mounted.current) {
          setError(failure);
          setBlocked(true);
        }
        throw failure;
      } finally {
        flight.current = null;
        if (mounted.current) setSaving(false);
      }
    },
    [organisation, client],
  );
  useEffect(() => {
    if (!dirty || blocked || saving || !server.canEdit) return;
    const timer = setTimeout(() => {
      void save().catch(() => undefined);
    }, 1200);
    return () => clearTimeout(timer);
  }, [dirty, blocked, saving, server.canEdit, save]);
  return {
    server,
    document,
    dirty,
    saving,
    error,
    blocked,
    conflict: error instanceof ApiError && error.status === 409,
    update,
    adopt,
    save,
  };
}
