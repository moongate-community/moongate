{{- define "moongate.fullname" -}}
{{- if contains "moongate" .Release.Name -}}
{{- .Release.Name | trunc 40 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-moongate" .Release.Name | trunc 40 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "moongate.labels" -}}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
app.kubernetes.io/name: moongate
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/* selectorLabels: dict "root" . "component" "login" */}}
{{- define "moongate.selectorLabels" -}}
app.kubernetes.io/name: moongate
app.kubernetes.io/instance: {{ .root.Release.Name }}
app.kubernetes.io/component: {{ .component }}
{{- end -}}

{{- define "moongate.image" -}}
{{- printf "%s:%s" .Values.image.repository (default .Chart.AppVersion .Values.image.tag) -}}
{{- end -}}

{{- define "moongate.secretName" -}}
{{- default (include "moongate.fullname" .) .Values.secrets.existingSecret -}}
{{- end -}}

{{- define "moongate.generatedName" -}}
{{- printf "%s-generated" (include "moongate.fullname" .) -}}
{{- end -}}

{{/* Database name of a realm id: realm-1 -> moongate_realm_1 */}}
{{- define "moongate.dbName" -}}
{{- printf "moongate_%s" (. | replace "-" "_") -}}
{{- end -}}

{{- define "moongate.validate" -}}
{{- $ids := dict -}}
{{- $indexes := dict -}}
{{- $pings := 0 -}}
{{- range .Values.realms -}}
{{- if hasKey $ids .id }}{{ fail (printf "duplicate realm id: %s" .id) }}{{ end -}}
{{- $_ := set $ids .id true -}}
{{- $key := printf "%d" (int .serverIndex) -}}
{{- if hasKey $indexes $key }}{{ fail (printf "duplicate serverIndex: %s" $key) }}{{ end -}}
{{- $_ := set $indexes $key true -}}
{{- if and .ping .ping.enabled }}{{ $pings = add1 $pings }}{{ end -}}
{{- end -}}
{{- if gt $pings 1 }}{{ fail "ping can be enabled on one realm only: UDP 12000 is one per address" }}{{ end -}}
{{- end -}}
