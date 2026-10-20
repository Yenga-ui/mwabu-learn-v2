export const resourceTypes = [
  { value: "lesson", label: "Lesson" },
  { value: "lesson-plan", label: "Lesson plan" },
  { value: "teacher-guide", label: "Teacher guide" },
  { value: "interactive-activity", label: "Interactive activity" },
  { value: "audio", label: "Audio" },
  { value: "video", label: "Video" },
];

export const resourceLanguages = [
  { value: "en", label: "English" },
  { value: "bem", label: "Bemba" },
  { value: "nya", label: "Chinyanja" },
  { value: "toi", label: "Tonga" },
  { value: "loz", label: "Lozi" },
  { value: "lun", label: "Lunda" },
  { value: "lue", label: "Luvale" },
  { value: "kqn", label: "Kaonde" },
];

// Keep existing catalogue values selectable without silently reclassifying them.
export function withExistingOption(
  options: { value: string; label: string }[],
  value?: string,
) {
  if (!value || options.some((option) => option.value === value))
    return options;
  return [
    ...options,
    {
      value,
      label: value === "activity" ? "Activity" : value.replaceAll("-", " "),
    },
  ];
}
