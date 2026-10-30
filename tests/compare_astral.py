import csv
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from astral import Observer
from astral.sun import sunrise, sunset

errors = []
events_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "build" / "tests" / "solar-events.tsv"
with events_path.open() as f:
    for row in csv.DictReader(f, delimiter="\t"):
        expected_event = sunrise if row["day"] == "True" else sunset
        # .NET writes 7 fractional digits; datetime accepts and truncates to microseconds.
        actual = datetime.fromisoformat(row["utc"].replace("Z", "+00:00"))
        observer = Observer(float(row["latitude"]), float(row["longitude"]))
        candidates = []
        for offset in (-1, 0, 1):
            try:
                candidates.append(expected_event(observer, date=actual.date()+timedelta(days=offset), tzinfo=timezone.utc))
            except ValueError:
                pass
        assert candidates, row
        error = min(abs((actual-candidate).total_seconds()) for candidate in candidates)
        errors.append(error)
        assert error < 120, (row, error)
print(f"PASS: {len(errors)} sunrise/sunset events compared with Astral 3.2; max difference {max(errors):.1f} seconds; tolerance 120 seconds.")
