import tickList from '../../components/TickList.module.css';
import { strings } from '../../copy/strings.ts';

/** The three Attendance ticks of a note, each independent of the others (D104). */
export type Attendance = { isNotIn: boolean; isLateNotice: boolean; isSick: boolean };

type AttendanceFieldProps = {
  attendance: Attendance;
  /** One tick changed, a change for autosave. */
  onChange: (attendance: Attendance) => void;
  className?: string;
};

const ticks = [
  { key: 'isNotIn', label: strings.noteWords.notIn },
  { key: 'isLateNotice', label: strings.noteWords.lateNotice },
  { key: 'isSick', label: strings.noteWords.sick },
] as const;

/**
 * The note form's Attendance (note-form.md component 4b), at the top: a `<fieldset>` whose legend is the `<h2>`
 * "Attendance", then three independent native tick boxes in the tick lists' rows, Not In, Late Notice and Sick, as the
 * Word template names them. They hide nothing: a participant who was not in still gets a note, and its Daily Support
 * Notes stay required.
 */
export function AttendanceField({ attendance, onChange, className }: AttendanceFieldProps) {
  return (
    <fieldset className={className === undefined ? tickList.group : `${tickList.group} ${className}`}>
      <legend className={tickList.legend}>
        <h2 className={tickList.heading}>{strings.note.attendance}</h2>
      </legend>
      {ticks.map(({ key, label }) => (
        <label key={key} htmlFor={`attendance-${key}`} className={tickList.row}>
          <input
            id={`attendance-${key}`}
            type="checkbox"
            className={tickList.box}
            name="attendance"
            checked={attendance[key]}
            onChange={(event) => {
              onChange({ ...attendance, [key]: event.currentTarget.checked });
            }}
          />
          <span className={tickList.text}>{label}</span>
        </label>
      ))}
    </fieldset>
  );
}
