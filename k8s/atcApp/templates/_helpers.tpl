{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "atcApp.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "atcApp.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "atcApp.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Define common, Kubernetes-related environment variables
*/}}
{{- define "atcApp.k8s.envvars" -}}
- name: SOURCE_CONTAINER_NAME
  value: {{ .service_name }}
- name: POD_NAME
  valueFrom:
    fieldRef:
      fieldPath: metadata.name
- name: NAMESPACE_NAME
  valueFrom:
    fieldRef:
      fieldPath: metadata.namespace
- name: POD_ID
  valueFrom:
    fieldRef:
      fieldPath: metadata.uid
{{- if eq .Values.metrics_mode "pull" }}
- name: METRICS_PORT
  value: {{ quote .service_port }}
{{- end }}
{{- end }}

{{- define "atcApp.fluentdSidecar" -}}
- name: fluentdsidecar
  image: {{ .Values.container_registry }}/fluentdsidecar:{{ .Values.image_tag | trim }}
  imagePullPolicy: Always
  env:
    - name: INSTRUMENTATION_KEY
      valueFrom:
        secretKeyRef:
          name: atc-secrets
          key: appinsights_instrumentationkey
{{ include "atcApp.k8s.envvars" . | indent 4 }}
  volumeMounts:
  - name: varlog
    mountPath: /var/log
  - name: varlibdockercontainers
    mountPath: /var/lib/docker/containers
    readOnly: true
  - name: emptydir
    mountPath: /var/fluentdsidecar
{{- end }}

{{- define "atcApp.telegrafSidecar" -}}
- name: telegrafsidecar
  image: telegraf:1.7.0
  imagePullPolicy: Always
  env:
    - name: APPINSIGHTS_INSTRUMENTATIONKEY
      valueFrom:
        secretKeyRef:
          name: atc-secrets
          key: appinsights_instrumentationkey
{{ include "atcApp.k8s.envvars" . | indent 4 }}
  volumeMounts:
  - name: config
    mountPath: /etc/telegraf
{{- end }}

{{- define "atcApp.telegrafConfigVolume" -}}
- name: config
  configMap:
    name: config-files
    items:
    - key: telegraf.conf
      path: telegraf.conf
{{- end }}

{{- define "atcApp.fluentdConsoleLogVolume" -}}
- name: varlog
  hostPath:
    path: /var/log
- name: varlibdockercontainers
  hostPath:
    path: /var/lib/docker/containers
- name: emptydir
  emptyDir: {}
{{- end }}

{{- define "atcApp.std.labels" -}}
app: atcApp
component: {{ .service_name }}
release: {{ .Release.Name }}
{{- end }}
