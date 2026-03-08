#!/usr/bin/env bash
# check-analyzers.sh
# Builds the solution and prints deduplicated SCA diagnostics.
# Output format (parseable):
#   TOTAL:<n>
#   RULE:<code>:<count>
#   FILE:<relative-path>:<count>
#   BLOCKER:<raw error line>

SLN="C:/Users/user/Documents/Unity/Scaffold/Scaffold.sln"
OUT=$(dotnet build "$SLN" --no-incremental 2>&1)

# Deduplicated SCA lines only
SCA_LINES=$(echo "$OUT" | grep -E ": (warning|error) SCA[0-9]+" | sort -u)

TOTAL=$(echo "$SCA_LINES" | grep -c "SCA" 2>/dev/null || echo 0)
echo "TOTAL:$TOTAL"

# Per-rule counts — extract only the first SCA code per line (appears twice in output)
echo "$SCA_LINES" \
  | sed -E 's/.*\s(SCA[0-9]+).*/\1/' \
  | sort | uniq -c | sort -rn \
  | awk '{print "RULE:"$2":"$1}'

# Per-file counts — match only paths followed by ( to exclude .csproj references
echo "$SCA_LINES" \
  | grep -o "Scaffold[/\\\\][^][(]*\.cs(" \
  | sed 's/($//' \
  | sed 's|.*Scaffold[/\\]||' \
  | sort | uniq -c | sort -rn \
  | awk '{print "FILE:"$2":"$1}'

# Non-SCA build errors (blockers)
BLOCKERS=$(echo "$OUT" | grep -E ": error " | grep -v "SCA" | grep -v "MSB" || true)
if [ -n "$BLOCKERS" ]; then
  echo "$BLOCKERS" | while IFS= read -r line; do echo "BLOCKER:$line"; done
fi
