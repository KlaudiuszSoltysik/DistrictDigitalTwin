terraform {
  backend "remote" {
    organization = "klaudiusz_soltysik_development"

    workspaces {
      name = "district-digital-twin"
    }
  }

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.40.0"
    }
    random = {
      source = "hashicorp/random"
    }
  }
}

variable "cloudflare_api_token" {
  description = "API Token"
  sensitive   = true
}

variable "account_id" {
  description = "Account ID"
}

variable "zone_id" {
  description = "Zone ID"
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

resource "random_id" "tunnel_secret" {
  byte_length = 32
}

resource "cloudflare_zero_trust_tunnel_cloudflared" "ddt_tunnel" {
  account_id = var.account_id
  name       = "district-digital-twin-tunnel"
  secret     = random_id.tunnel_secret.b64_std
  config_src = "cloudflare"
}

resource "cloudflare_zero_trust_tunnel_cloudflared_config" "ddt_config" {
  account_id = var.account_id
  tunnel_id  = cloudflare_zero_trust_tunnel_cloudflared.ddt_tunnel.id

  config {
    ingress_rule {
      hostname = "district-digital-twin.260824.xyz"
      service  = "http://website:80"
    }
    ingress_rule {
      hostname = "district-digital-twin-grafana.260824.xyz"
      service  = "http://grafana:3000"
    }
    ingress_rule {
      service = "http_status:404"
    }
  }
}

resource "cloudflare_record" "ddt_dns" {
  zone_id = var.zone_id
  name    = "district-digital-twin"
  content= "${cloudflare_zero_trust_tunnel_cloudflared.ddt_tunnel.id}.cfargotunnel.com"
  type    = "CNAME"
  proxied = true
}

resource "cloudflare_record" "grafana_dns" {
  zone_id = var.zone_id
  name    = "district-digital-twin-grafana"
  content = "${cloudflare_zero_trust_tunnel_cloudflared.ddt_tunnel.id}.cfargotunnel.com"
  type    = "CNAME"
  proxied = true
}

resource "cloudflare_zero_trust_access_application" "grafana_access" {
  account_id       = var.account_id
  name             = "District Digital Twin Grafana"
  domain           = "district-digital-twin-grafana.260824.xyz"
  type             = "self_hosted"
  session_duration = "1000h"
}

resource "cloudflare_zero_trust_access_policy" "grafana_policy" {
  account_id     = var.account_id
  application_id = cloudflare_zero_trust_access_application.grafana_access.id
  name           = "Allow Only Me"
  precedence     = "1"
  decision       = "allow"

  include {
    email = ["klaudiusz.s1405@gmail.com"]
  }
}

output "tunnel_token" {
  sensitive = true
  value = base64encode(jsonencode({
    "a" = var.account_id,
    "t" = cloudflare_zero_trust_tunnel_cloudflared.ddt_tunnel.id,
    "s" = random_id.tunnel_secret.b64_std
  }))
}
