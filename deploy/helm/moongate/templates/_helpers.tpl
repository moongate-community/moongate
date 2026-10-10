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
app.kubernetes.io/instance: {{ .Release.Name | quote }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{/* selectorLabels: dict "root" . "component" "login" */}}
{{- define "moongate.selectorLabels" -}}
app.kubernetes.io/name: moongate
app.kubernetes.io/instance: {{ .root.Release.Name | quote }}
app.kubernetes.io/component: {{ .component | quote }}
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
{{- $root := . -}}
{{- $reserved := list "accounts" "login" "postgresql" "redis" "generated" -}}
{{- $ids := dict -}}
{{- $indexes := dict -}}
{{- $pings := 0 -}}
{{- range .Values.realms -}}
{{- if has .id $reserved }}{{ fail (printf "reserved realm id: %s (reserved: %s)" .id (join ", " $reserved)) }}{{ end -}}
{{- if hasKey $ids .id }}{{ fail (printf "duplicate realm id: %s" .id) }}{{ end -}}
{{- $_ := set $ids .id true -}}
{{- $key := printf "%d" (int .serverIndex) -}}
{{- if hasKey $indexes $key }}{{ fail (printf "duplicate serverIndex: %s" $key) }}{{ end -}}
{{- $_ := set $indexes $key true -}}
{{- if and .ping .ping.enabled }}{{ $pings = add1 $pings }}{{ end -}}
{{- /* A StatefulSet name leaves 11 characters for the controller-revision-hash label. */ -}}
{{- $name := printf "%s-%s" (include "moongate.fullname" $root) .id -}}
{{- if gt (len $name) 52 }}{{ fail (printf "the name %s is too long (%d characters, 52 at most): use a shorter release name or realm id" $name (len $name)) }}{{ end -}}
{{- $svc := default dict .service -}}
{{- if eq (default "LoadBalancer" $svc.type) "NodePort" -}}
{{- $port := int (default 2595 .advertisedPort) -}}
{{- if or (lt $port 30000) (gt $port 32767) }}{{ fail (printf "realm %s: with service.type NodePort the advertisedPort is the node port and must be in 30000-32767, not %d" .id $port) }}{{ end -}}
{{- end -}}
{{- end -}}
{{- if gt $pings 1 }}{{ fail "ping can be enabled on one realm only: UDP 12000 is one per address" }}{{ end -}}
{{- end -}}

{{/* podSecurityContext */}}
{{- define "moongate.podSecurityContext" -}}
runAsNonRoot: true
runAsUser: {{ .Values.podSecurity.runAsUser }}
runAsGroup: {{ .Values.podSecurity.runAsUser }}
fsGroup: {{ .Values.podSecurity.fsGroup }}
seccompProfile:
  type: RuntimeDefault
{{- end -}}

{{- define "moongate.containerSecurityContext" -}}
allowPrivilegeEscalation: false
capabilities:
  drop: ["ALL"]
{{- end -}}

{{/* secretEnv: dict "name" ENV "secret" SECRETNAME "key" KEY */}}
{{- define "moongate.secretEnv" -}}
- name: {{ .name }}
  valueFrom:
    secretKeyRef:
      name: {{ .secret }}
      key: {{ .key }}
{{- end -}}

{{/*
  initContainers for a pod: dict "root" . "target" auth|world "configMap" NAME "schemaKey" KEY ("<id>-schema-url") "schemaEnv" ENV
*/}}
{{- define "moongate.initContainers" -}}
- name: init-root
  image: {{ include "moongate.image" .root }}
  imagePullPolicy: {{ .root.Values.image.pullPolicy }}
  command: ["/app/mgctl", "init", "/data"]
  securityContext:
    {{- include "moongate.containerSecurityContext" .root | nindent 4 }}
  volumeMounts:
    - {name: data, mountPath: /data}
    - {name: config, mountPath: /data/config/moongate.toml, subPath: moongate.toml}
{{- if .root.Values.schema.enabled }}
- name: migrate
  image: {{ include "moongate.image" .root }}
  imagePullPolicy: {{ .root.Values.image.pullPolicy }}
  command:
    - /bin/sh
    - -ec
    - |
      mkdir -p /tmp/schema/config
      cp /schema-config/schema.toml /tmp/schema/config/moongate.toml
      exec /app/mgctl migrate apply --root-directory /tmp/schema --target {{ .target }}
  env:
    {{- include "moongate.secretEnv" (dict "name" .schemaEnv "secret" (include "moongate.secretName" .root) "key" .schemaKey) | nindent 4 }}
  securityContext:
    {{- include "moongate.containerSecurityContext" .root | nindent 4 }}
  volumeMounts:
    - {name: tmp, mountPath: /tmp}
    - {name: config, mountPath: /schema-config/schema.toml, subPath: schema.toml}
{{- end }}
{{- end -}}
