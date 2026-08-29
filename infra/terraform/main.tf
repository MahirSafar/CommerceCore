# Terraform skeleton for CommerceCore GCP Infrastructure
terraform {
  required_version = ">= 1.9.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }
  }
  # backend "gcs" {
  #   bucket = "commercecore-tfstate"
  #   prefix = "terraform/state"
  # }
}

provider "google" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

variable "gcp_project_id" {
  type        = string
  description = "GCP Project ID"
  default     = "commercecore-dev"
}

variable "gcp_region" {
  type        = string
  description = "GCP primary region"
  default     = "europe-west1"
}
