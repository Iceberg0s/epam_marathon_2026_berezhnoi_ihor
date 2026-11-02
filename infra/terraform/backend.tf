terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket = "terraform-state-113415263208"
    key          = "restaurant/terraform.tfstate"
    region = "us-east-1"
    use_lockfile = true
    encrypt      = true
  }
}
