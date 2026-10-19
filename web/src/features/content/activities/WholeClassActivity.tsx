import { useEffect, useRef } from "react";
import { mountActivity, type ActivityDefinition } from "./activityRuntime";
import { mountWholeClass } from "./wholeClassRuntime";
export function WholeClassActivity({
  definition,
}: {
  definition: ActivityDefinition;
}) {
  const host = useRef<HTMLElement>(null);
  useEffect(() => {
    if (host.current)
      return mountWholeClass(host.current, definition, mountActivity);
  }, [definition]);
  return <section ref={host} aria-label="Whole-class activity" />;
}
