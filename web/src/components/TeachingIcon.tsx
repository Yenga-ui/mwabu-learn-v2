import {
  BookOpen,
  Calculator,
  FlaskConical,
  Globe,
  Languages,
  Palette,
} from "lucide-react";

/** Decorative cues supplement the visible subject name; labels remain authoritative. */
export default function TeachingIcon({ subject }: { subject?: string }) {
  const name = subject?.toLowerCase() ?? "";
  const Icon = /math|numeracy/.test(name)
    ? Calculator
    : /science/.test(name)
      ? FlaskConical
      : /english|language|literacy/.test(name)
        ? Languages
        : /creative|technology|art/.test(name)
          ? Palette
          : /social|geograph/.test(name)
            ? Globe
            : BookOpen;
  return <Icon size={24} aria-hidden="true" className="teaching-tile-icon" />;
}
